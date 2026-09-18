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
    public sealed record SetLightRequest(bool? On, int? Brightness, string? ColorHex);

    public static IEndpointRouteBuilder MapLightEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/lights")
            .RequireAuthorization(TokenAuthenticationHandler.ClientPolicy);

        group.MapGet("/", GetLights);
        group.MapPut("/{id}", SetLight);

        return app;
    }

    private static async Task<IResult> GetLights(
        RoomOsDbContext db, StateStore state, CancellationToken ct) =>
        Results.Ok(await BuildSnapshotsAsync(db, state, ct));

    /// <summary>
    /// Construit la liste des lampes. Partagé avec <c>GET /api/state</c> : les lampes
    /// déclarées sont visibles même avant tout message de Zigbee2MQTT, marquées
    /// injoignables — sinon la carte serait vide au démarrage.
    /// </summary>
    public static async Task<List<LightSnapshot>> BuildSnapshotsAsync(
        RoomOsDbContext db, StateStore state, CancellationToken ct)
    {
        var devices = await db.Devices.AsNoTracking()
            .Where(d => d.Kind == DeviceKind.Light && d.Enabled)
            .OrderBy(d => d.Id)
            .ToListAsync(ct);

        return [.. devices.Select(device =>
        {
            var config = JsonSerializer.Deserialize<LightConfig>(device.ConfigJson);
            var live = state.GetLight(device.Id);

            return new LightSnapshot(
                device.Id,
                device.Name,
                live?.On ?? false,
                live?.Brightness,
                live?.ColorHex,
                live?.Reachable ?? false,
                Paired: live is not null,
                config?.SupportsColor ?? false,
                config?.SupportsBrightness ?? true);
        })];
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

        if (payload.Count == 0)
        {
            return Results.BadRequest(new { message = "Rien à changer." });
        }

        // Pas de mise à jour optimiste : l'état affiché viendra du message que
        // Zigbee2MQTT republiera après exécution.
        return await mqtt.PublishSetAsync(config.Z2mFriendlyName, payload, ct)
            ? Results.Accepted()
            : Results.Conflict(new { message = "Le pont MQTT n'est pas connecté." });
    }
}
