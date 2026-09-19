using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RoomOS.Core.Data;
using RoomOS.Core.Data.Entities;
using RoomOS.Core.Integrations.Mqtt;
using RoomOS.Core.State;
using RoomOS.Domain.Contracts;

namespace RoomOS.Core.Scenes.Executors;

public sealed class LightSetExecutor(
    IServiceScopeFactory scopeFactory, MqttLightService mqtt, StateStore state) : IStepExecutor
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

        // Exclusive de la couleur : une lampe est dans l'un ou l'autre mode.
        if (step.ColorTempMired is { } mired && step.ColorHex is null)
        {
            payload["color_temp"] = mired;
        }

        if (step.Effect is { } effect)
        {
            payload["effect"] = effect;
        }

        if (payload.Count == 0)
        {
            return;
        }

        if (step.TransitionSec is { } transition and >= 0 and <= 60)
        {
            payload["transition"] = transition;
        }

        if (!await mqtt.PublishSetAsync(config.Z2mFriendlyName, payload, ct))
        {
            throw new InvalidOperationException("Le pont MQTT n'est pas connecté.");
        }

        // La lampe republie son état après exécution : on l'attend, sinon l'étape
        // réussirait même sans ampoule au bout (docs/03-architecture.md, règle 5).
        // Seul l'allumage est vérifié : la luminosité et la couleur arrivent par
        // paliers pendant un fondu, et les comparer exactement ferait échouer une
        // étape parfaitement appliquée.
        if (step.On is { } expected)
        {
            await StateWaiter.UntilAsync(() => state.GetLight(id)?.On == expected, ct);
        }
    }
}
