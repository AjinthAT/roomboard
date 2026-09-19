using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RoomOS.Core.Auth;
using RoomOS.Core.Data;
using RoomOS.Core.Data.Entities;
using RoomOS.Core.Integrations.Mqtt;
using RoomOS.Core.State;
using RoomOS.Domain.Contracts;

namespace RoomOS.Core.Api;

public static class LightEndpoints
{
    public sealed record SetLightRequest(
        bool? On,
        int? Brightness,
        string? ColorHex,
        int? ColorTempMired,
        string? Effect,
        string? PowerOnBehavior,
        double? TransitionSec);

    public static IEndpointRouteBuilder MapLightEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/lights")
            .RequireAuthorization(TokenAuthenticationHandler.ClientPolicy);

        group.MapGet("/", GetLights);
        group.MapPut("/{id}", SetLight);
        group.MapPost("/{id}/identify", Identify);

        return app;
    }

    private static async Task<IResult> GetLights(
        RoomOsDbContext db, StateStore state, MqttLightService mqtt, CancellationToken ct) =>
        Results.Ok(await BuildSnapshotsAsync(db, state, mqtt, ct));

    /// <summary>
    /// Construit la liste des lampes. Partagé avec <c>GET /api/state</c> : les lampes
    /// déclarées sont visibles même avant tout message de Zigbee2MQTT, marquées
    /// injoignables — sinon la carte serait vide au démarrage.
    /// </summary>
    public static async Task<List<LightSnapshot>> BuildSnapshotsAsync(
        RoomOsDbContext db, StateStore state, MqttLightService mqtt, CancellationToken ct)
    {
        var devices = await db.Devices.AsNoTracking()
            .Where(d => d.Kind == DeviceKind.Light && d.Enabled)
            .OrderBy(d => d.Id)
            .ToListAsync(ct);

        return [.. devices.Select(device =>
        {
            var config = JsonSerializer.Deserialize<LightConfig>(device.ConfigJson);
            var live = state.GetLight(device.Id);

            // « Appairée » vient de l'inventaire publié par Zigbee2MQTT, pas de la
            // réception d'un message : une lampe appairée mais immobile depuis le
            // démarrage du Core est bien appairée, et une lampe partie ne l'est plus.
            var paired = config is not null && mqtt.IsPaired(config.Z2mFriendlyName);

            // Les capacités viennent de l'inventaire Zigbee. La configuration ne sert
            // que de repli avant sa réception, et pour une lampe jamais vue.
            var caps = config is null ? LightCapabilities.None : mqtt.GetCapabilities(config.Z2mFriendlyName);

            if (!paired)
            {
                caps = caps with
                {
                    Brightness = config?.SupportsBrightness ?? true,
                    Color = config?.SupportsColor ?? false,
                };
            }

            return new LightSnapshot(
                device.Id,
                device.Name,
                live?.On ?? false,
                live?.Brightness,
                live?.ColorHex,
                live?.Reachable ?? false,
                Paired: paired,
                live?.ColorTempMired,
                live?.LinkQuality,
                live?.PowerOnBehavior,
                caps);
        })];
    }

    /// <summary>
    /// Fait clignoter la lampe. Sans usage avec une seule ampoule, indispensable
    /// pour savoir laquelle est laquelle dès qu'il y en a plusieurs.
    /// </summary>
    private static async Task<IResult> Identify(
        string id, RoomOsDbContext db, MqttLightService mqtt, CancellationToken ct)
    {
        var name = await FriendlyNameAsync(id, db, ct);

        if (name is null)
        {
            return Results.NotFound(new { message = $"Lampe « {id} » inconnue." });
        }

        return await mqtt.PublishSetAsync(name, new { effect = "blink" }, ct)
            ? Results.Accepted()
            : Results.Conflict(new { message = "Le pont MQTT n'est pas connecté." });
    }

    private static async Task<string?> FriendlyNameAsync(
        string id, RoomOsDbContext db, CancellationToken ct)
    {
        var device = await db.Devices.AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id && d.Kind == DeviceKind.Light, ct);

        return device is null
            ? null
            : JsonSerializer.Deserialize<LightConfig>(device.ConfigJson)?.Z2mFriendlyName;
    }

    private static async Task<IResult> SetLight(
        string id,
        SetLightRequest request,
        RoomOsDbContext db,
        MqttLightService mqtt,
        CancellationToken ct)
    {
        if (request.Brightness is < 0 or > 100)
        {
            return Results.BadRequest(new { message = "La luminosité va de 0 à 100." });
        }

        if (request.ColorHex is not null && !ZigbeeColor.IsValidHex(request.ColorHex))
        {
            // Z2M ignore silencieusement une couleur malformée : mieux vaut refuser ici.
            return Results.BadRequest(new { message = "La couleur doit être au format #RRGGBB." });
        }

        if (request.ColorTempMired is < 100 or > 600)
        {
            return Results.BadRequest(new { message = "La température va de 100 à 600 mireds." });
        }

        var device = await db.Devices.AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id && d.Kind == DeviceKind.Light, ct);

        if (device is null)
        {
            return Results.NotFound(new { message = $"Lampe « {id} » inconnue." });
        }

        var config = JsonSerializer.Deserialize<LightConfig>(device.ConfigJson);

        if (config is null || string.IsNullOrWhiteSpace(config.Z2mFriendlyName))
        {
            return Results.Problem($"La lampe « {id} » n'a pas de nom Zigbee2MQTT configuré.");
        }

        var payload = new Dictionary<string, object>();

        if (request.On is { } on)
        {
            payload["state"] = on ? "ON" : "OFF";
        }

        if (request.Brightness is { } brightness)
        {
            payload["brightness"] = ZigbeeColor.ToZigbeeBrightness(brightness);
        }

        if (request.ColorHex is { } hex)
        {
            payload["color"] = new { hex };
        }

        // Couleur et température s'excluent : une lampe est dans l'un ou l'autre mode.
        // Envoyer les deux laisserait le dernier arrivé gagner, de façon imprévisible.
        if (request.ColorTempMired is { } mired && request.ColorHex is null)
        {
            payload["color_temp"] = mired;
        }

        if (request.Effect is { } effect)
        {
            payload["effect"] = effect;
        }

        if (request.PowerOnBehavior is { } behaviour)
        {
            payload["power_on_behavior"] = behaviour;
        }

        if (payload.Count == 0)
        {
            return Results.BadRequest(new { message = "Rien à changer." });
        }

        // Durée de fondu, en secondes. Z2M l'applique à l'ensemble de la commande.
        if (request.TransitionSec is { } transition and >= 0 and <= 60)
        {
            payload["transition"] = transition;
        }

        // Pas de mise à jour optimiste : l'état affiché viendra du message que
        // Zigbee2MQTT republiera après exécution.
        return await mqtt.PublishSetAsync(config.Z2mFriendlyName, payload, ct)
            ? Results.Accepted()
            : Results.Conflict(new { message = "Le pont MQTT n'est pas connecté." });
    }
}
