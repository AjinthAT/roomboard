using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MQTTnet;
using RoomOS.Core.Configuration;
using RoomOS.Core.Data;
using RoomOS.Core.Data.Entities;
using RoomOS.Core.State;
using RoomOS.Domain.Contracts;

namespace RoomOS.Core.Integrations.Mqtt;

/// <summary>
/// Pont entre Zigbee2MQTT et le <see cref="StateStore"/>.
/// </summary>
/// <remarks>
/// RoomOS ne parle jamais Zigbee : il s'abonne à <c>zigbee2mqtt/+</c> pour recevoir
/// les états et publie sur <c>zigbee2mqtt/&lt;nom&gt;/set</c> pour commander. C'est
/// ce découplage qui permet de garder l'affichage juste même quand une lampe est
/// pilotée par son interrupteur physique (docs/09-integrations.md).
/// </remarks>
public sealed class MqttLightService(
    IServiceScopeFactory scopeFactory,
    StateStore state,
    IOptions<RoomOsOptions> options,
    TimeProvider time,
    ILogger<MqttLightService> logger) : BackgroundService
{
    private readonly MqttOptions _mqtt = options.Value.Mqtt;

    /// <summary>Nom convivial Zigbee2MQTT vers identifiant RoomOS.</summary>
    private readonly ConcurrentDictionary<string, string> _deviceIdByFriendlyName = new();

    private IMqttClient? _client;
    private bool _warnedDisconnected;

    public bool IsConnected => _client?.IsConnected ?? false;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await LoadDeviceMapAsync(stoppingToken);

        if (_deviceIdByFriendlyName.IsEmpty)
        {
            logger.LogInformation("Aucune lampe déclarée : le pont MQTT reste inactif.");
            return;
        }

        var factory = new MqttClientFactory();
        _client = factory.CreateMqttClient();

        _client.ApplicationMessageReceivedAsync += OnMessageAsync;

        var clientOptions = new MqttClientOptionsBuilder()
            .WithTcpServer(_mqtt.Host, _mqtt.Port)
            .WithClientId("roomos-core")
            .WithCleanSession()
            .Build();

        // Boucle de reconnexion maison : le client MQTT ne se reconnecte pas seul, et
        // Mosquitto peut redémarrer indépendamment du Core.
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!_client.IsConnected)
                {
                    await _client.ConnectAsync(clientOptions, stoppingToken);
                    await SubscribeAsync(stoppingToken);

                    _warnedDisconnected = false;
                    logger.LogInformation("Connecté à Mosquitto sur {Host}:{Port}.", _mqtt.Host, _mqtt.Port);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                if (!_warnedDisconnected)
                {
                    _warnedDisconnected = true;
                    logger.LogWarning(ex, "Connexion à Mosquitto impossible, tentatives poursuivies.");
                }
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private async Task LoadDeviceMapAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RoomOsDbContext>();

        var lights = await db.Devices.AsNoTracking()
            .Where(d => d.Kind == DeviceKind.Light && d.Enabled)
            .ToListAsync(ct);

        foreach (var light in lights)
        {
            var config = JsonSerializer.Deserialize<LightConfig>(light.ConfigJson);

            if (config is not null && !string.IsNullOrWhiteSpace(config.Z2mFriendlyName))
            {
                _deviceIdByFriendlyName[config.Z2mFriendlyName] = light.Id;
            }
        }
    }

    private async Task SubscribeAsync(CancellationToken ct)
    {
        // Un seul niveau de joker : zigbee2mqtt/<nom> et zigbee2mqtt/<nom>/availability.
        await _client!.SubscribeAsync($"{_mqtt.BaseTopic}/+", cancellationToken: ct);
        await _client.SubscribeAsync($"{_mqtt.BaseTopic}/+/availability", cancellationToken: ct);
    }

    private Task OnMessageAsync(MqttApplicationMessageReceivedEventArgs e)
    {
        var topic = e.ApplicationMessage.Topic;
        var payload = e.ApplicationMessage.ConvertPayloadToString() ?? string.Empty;

        var segments = topic.Split('/');

        if (segments.Length < 2 || segments[0] != _mqtt.BaseTopic)
        {
            return Task.CompletedTask;
        }

        var friendlyName = segments[1];

        if (!_deviceIdByFriendlyName.TryGetValue(friendlyName, out var deviceId))
        {
            // Zigbee2MQTT publie aussi bridge/state, bridge/devices, etc.
            return Task.CompletedTask;
        }

        try
        {
            if (segments.Length == 3 && segments[2] == "availability")
            {
                ApplyAvailability(deviceId, payload);
            }
            else if (segments.Length == 2)
            {
                ApplyState(deviceId, payload);
            }
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Charge utile illisible sur {Topic} : {Payload}", topic, payload);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Zigbee2MQTT publie la disponibilité soit en JSON (<c>{"state":"online"}</c>),
    /// soit en texte brut selon sa configuration. Les deux formes sont acceptées.
    /// </summary>
    private void ApplyAvailability(string deviceId, string payload)
    {
        var reachable = payload.Contains("online", StringComparison.OrdinalIgnoreCase);
        var previous = state.GetLight(deviceId);

        state.SetLight(deviceId, (previous ?? LightState.Unknown(time.GetUtcNow())) with
        {
            Reachable = reachable,
            UpdatedAt = time.GetUtcNow(),
        });
    }

    private void ApplyState(string deviceId, string payload)
    {
        var json = JsonSerializer.Deserialize<JsonElement>(payload);

        if (json.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        var previous = state.GetLight(deviceId);

        var on = json.TryGetProperty("state", out var s)
            ? string.Equals(s.GetString(), "ON", StringComparison.OrdinalIgnoreCase)
            : previous?.On ?? false;

        var brightness = json.TryGetProperty("brightness", out var b) && b.ValueKind == JsonValueKind.Number
            ? ZigbeeColor.ToPercent(b.GetInt32())
            : previous?.Brightness;

        var colorHex = ReadColor(json) ?? previous?.ColorHex;

        state.SetLight(deviceId, new LightState(
            on, brightness, colorHex, previous?.Reachable ?? true, time.GetUtcNow()));
    }

    /// <summary>
    /// Zigbee2MQTT accepte une couleur en hexadécimal mais la renvoie en coordonnées
    /// CIE xy. Sans cette conversion, la pastille de couleur afficherait la dernière
    /// valeur commandée, pas la réalité.
    /// </summary>
    private static string? ReadColor(JsonElement json)
    {
        if (!json.TryGetProperty("color", out var color) || color.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (color.TryGetProperty("hex", out var hex) && hex.GetString() is { } value)
        {
            return value;
        }

        return color.TryGetProperty("x", out var x) && color.TryGetProperty("y", out var y)
            ? ZigbeeColor.FromXy(x.GetDouble(), y.GetDouble())
            : null;
    }

    /// <summary>Publie une commande sur <c>zigbee2mqtt/&lt;nom&gt;/set</c>.</summary>
    public async Task<bool> PublishSetAsync(string friendlyName, object payload, CancellationToken ct)
    {
        if (_client is null || !_client.IsConnected)
        {
            return false;
        }

        var message = new MqttApplicationMessageBuilder()
            .WithTopic($"{_mqtt.BaseTopic}/{friendlyName}/set")
            .WithPayload(JsonSerializer.Serialize(payload))
            .Build();

        await _client.PublishAsync(message, ct);
        return true;
    }

    public override void Dispose()
    {
        _client?.Dispose();
        base.Dispose();
    }
}
