using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RoomOS.Core.Data;
using RoomOS.Core.Data.Entities;
using RoomOS.Core.Integrations.Mqtt;
using RoomOS.Domain.Contracts;

namespace RoomOS.Core.Scenes.Executors;

public sealed class LightSetExecutor(
    IServiceScopeFactory scopeFactory, MqttLightService mqtt) : IStepExecutor
{
    public string Type => SceneStepTypes.LightSet;

    public async Task ExecuteAsync(SceneStep step, CancellationToken ct)
    {
        var id = step.DeviceId ?? throw new InvalidOperationException("deviceId manquant.");

        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RoomOsDbContext>();

        var device = await db.Devices.AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id && d.Kind == DeviceKind.Light, ct)
            ?? throw new InvalidOperationException($"Lampe « {id} » inconnue.");

        var config = JsonSerializer.Deserialize<LightConfig>(device.ConfigJson)
            ?? throw new InvalidOperationException($"Configuration illisible pour « {id} ».");

        var payload = new Dictionary<string, object>();

        if (step.On is { } on)
        {
            payload["state"] = on ? "ON" : "OFF";
        }

        if (step.Brightness is { } brightness)
        {
            payload["brightness"] = ZigbeeColor.ToZigbeeBrightness(brightness);
        }

        if (step.ColorHex is { } hex)
        {
            if (!ZigbeeColor.IsValidHex(hex))
            {
                throw new InvalidOperationException($"Couleur « {hex} » invalide.");
            }

            payload["color"] = new { hex };
        }

        if (payload.Count == 0)
        {
            return;
        }

        if (!await mqtt.PublishSetAsync(config.Z2mFriendlyName, payload, ct))
        {
            throw new InvalidOperationException("Le pont MQTT n'est pas connecté.");
        }
    }
}
