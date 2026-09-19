using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MQTTnet;
using RoomOS.Core.Configuration;
using RoomOS.Core.Data;
using RoomOS.Core.Data.Entities;
using RoomOS.Core.Json;
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

    /// <summary>
    /// Noms Z2M réellement appairés, d'après le sujet retenu <c>bridge/devices</c>.
    /// </summary>
    /// <remarks>
    /// Sans cette source, « appairée » se déduisait de la réception d'un message
    /// depuis le démarrage du Core. Deux erreurs en découlaient : une lampe bien
    /// appairée s'affichait « pas encore appairée » après chaque redémarrage du Core,
    /// tant qu'elle n'avait pas bougé ; et une lampe ayant quitté le réseau restait
    /// affichée comme appairée tant que le Core tournait.
    /// </remarks>
    private readonly HashSet<string> _pairedFriendlyNames = [];
    private readonly Dictionary<string, LightCapabilities> _capabilities = [];
    private readonly Lock _pairedGate = new();

    private IMqttClient? _client;
    private bool _warnedDisconnected;

    public bool IsConnected => _client?.IsConnected ?? false;

    /// <summary>L'appareil est-il présent dans l'inventaire publié par Zigbee2MQTT ?</summary>
    public bool IsPaired(string friendlyName)
    {
        lock (_pairedGate)
        {
            return _pairedFriendlyNames.Contains(friendlyName);
        }
    }

    /// <summary>Capacités déduites de l'inventaire, ou aucune si l'appareil est inconnu.</summary>
    public LightCapabilities GetCapabilities(string friendlyName)
    {
        lock (_pairedGate)
        {
            return _capabilities.TryGetValue(friendlyName, out var c) ? c : LightCapabilities.None;
        }
    }

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

        // Sujet retenu : il arrive dès la souscription, y compris après un
        // redémarrage du Core, et fait autorité sur ce qui est appairé.
        await _client.SubscribeAsync($"{_mqtt.BaseTopic}/bridge/devices", cancellationToken: ct);
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

        if (segments.Length == 3 && segments[1] == "bridge" && segments[2] == "devices")
        {
            ApplyInventory(payload);
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
    /// Inventaire publié par Zigbee2MQTT. Un appareil disparu de cette liste a quitté
    /// le réseau : son état connu est effacé plutôt que laissé à l'écran.
    /// </summary>
    private void ApplyInventory(string payload)
    {
        var devices = JsonSerializer.Deserialize<JsonElement>(payload);

        if (devices.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var names = devices.EnumerateArray()
            .Where(d => d.TryGetProperty("type", out var t) && t.GetString() != "Coordinator")
            .Select(d => d.TryGetProperty("friendly_name", out var n) ? n.GetString() : null)
            .Where(n => !string.IsNullOrEmpty(n))
            .Select(n => n!)
            .ToHashSet();

        var capabilities = devices.EnumerateArray()
            .Where(d => d.TryGetProperty("friendly_name", out _))
            .ToDictionary(
                d => d.GetProperty("friendly_name").GetString()!,
                ReadCapabilities);

        List<string> departed;

        lock (_pairedGate)
        {
            departed = [.. _pairedFriendlyNames.Except(names)];
            _pairedFriendlyNames.Clear();
            _pairedFriendlyNames.UnionWith(names);

            _capabilities.Clear();
            foreach (var (name, caps) in capabilities)
            {
                _capabilities[name] = caps;
            }
        }

        foreach (var name in departed)
        {
            if (_deviceIdByFriendlyName.TryGetValue(name, out var deviceId))
            {
                logger.LogWarning("La lampe {Name} a quitté le réseau Zigbee.", name);
                state.ForgetLight(deviceId);
            }
        }

        logger.LogInformation("Inventaire Zigbee : {Count} appareil(s) appairé(s).", names.Count);

        // Zigbee2MQTT ne republie l'état d'une lampe qu'au changement. Après un
        // redémarrage du Core, ses attributs resteraient donc inconnus jusqu'à ce que
        // quelqu'un touche l'interrupteur. On les redemande une fois.
        _ = RefreshAsync(names);
    }

    /// <summary>
    /// Interroge chaque lampe connue. Les ampoules sur secteur répondent même
    /// éteintes : elles restent alimentées, seul leur éclairage est coupé.
    /// </summary>
    private async Task RefreshAsync(IEnumerable<string> friendlyNames)
    {
        var wanted = new { state = "", brightness = "", color_temp = "", color = "" };

        foreach (var name in friendlyNames)
        {
            if (!_deviceIdByFriendlyName.ContainsKey(name))
            {
                continue;
            }

            try
            {
                await PublishAsync($"{_mqtt.BaseTopic}/{name}/get", wanted, CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Rafraîchissement de {Name} impossible.", name);
            }
        }
    }

    /// <summary>
    /// Lit les capacités depuis la description publiée par Zigbee2MQTT.
    /// </summary>
    /// <remarks>
    /// Les capacités d'éclairage sont imbriquées dans une entrée de type <c>light</c>,
    /// les autres réglages sont à plat. On parcourt les deux niveaux.
    /// </remarks>
    private static LightCapabilities ReadCapabilities(JsonElement device)
    {
        if (device.GetPropertyOrNull("definition")?.GetPropertyOrNull("exposes")
            is not { ValueKind: JsonValueKind.Array } exposes)
        {
            return LightCapabilities.None;
        }

        bool brightness = false, color = false, colorTemp = false;
        int? tempMin = null, tempMax = null;
        List<string> effects = [], powerOn = [];

        foreach (var expose in exposes.EnumerateArray())
        {
            var property = expose.GetPropertyOrNull("property")?.GetString();

            if (property == "effect")
            {
                effects = ReadValues(expose);
            }
            else if (property == "power_on_behavior")
            {
                powerOn = ReadValues(expose);
            }

            if (expose.GetPropertyOrNull("features") is not { ValueKind: JsonValueKind.Array } features)
            {
                continue;
            }

            foreach (var feature in features.EnumerateArray())
            {
                switch (feature.GetPropertyOrNull("property")?.GetString())
                {
                    case "brightness":
                        brightness = true;
                        break;
                    case "color":
                        color = true;
                        break;
                    case "color_temp":
                        colorTemp = true;
                        tempMin = feature.GetPropertyOrNull("value_min")?.GetInt32();
                        tempMax = feature.GetPropertyOrNull("value_max")?.GetInt32();
                        break;
                }
            }
        }

        return new LightCapabilities(brightness, color, colorTemp, tempMin, tempMax, effects, powerOn);
    }

    private static List<string> ReadValues(JsonElement expose) =>
        expose.GetPropertyOrNull("values") is { ValueKind: JsonValueKind.Array } values
            ? [.. values.EnumerateArray().Select(v => v.GetString()).Where(v => v is not null).Select(v => v!)]
            : [];

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

        var colorTemp = json.TryGetProperty("color_temp", out var ct) && ct.ValueKind == JsonValueKind.Number
            ? ct.GetInt32()
            : previous?.ColorTempMired;

        var linkQuality = json.TryGetProperty("linkquality", out var lq) && lq.ValueKind == JsonValueKind.Number
            ? lq.GetInt32()
            : previous?.LinkQuality;

        var powerOn = json.TryGetProperty("power_on_behavior", out var po)
            ? po.GetString() ?? previous?.PowerOnBehavior
            : previous?.PowerOnBehavior;

        state.SetLight(deviceId, new LightState(
            on, brightness, colorHex, previous?.Reachable ?? true, time.GetUtcNow(),
            colorTemp, linkQuality, powerOn));
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
    public Task<bool> PublishSetAsync(string friendlyName, object payload, CancellationToken ct) =>
        PublishAsync($"{_mqtt.BaseTopic}/{friendlyName}/set", payload, ct);

    private async Task<bool> PublishAsync(string topic, object payload, CancellationToken ct)
    {
        if (_client is null || !_client.IsConnected)
        {
            return false;
        }

        var message = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
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
