using Microsoft.EntityFrameworkCore;
using RoomOS.Core.Auth;
using RoomOS.Core.Data;
using RoomOS.Core.Data.Entities;
using RoomOS.Core.Integrations.Mqtt;
using RoomOS.Core.State;
using RoomOS.Domain.Contracts;

namespace RoomOS.Core.Api;

public static class StateEndpoints
{
    public static IEndpointRouteBuilder MapStateEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api")
            .RequireAuthorization(TokenAuthenticationHandler.ClientPolicy);

        group.MapGet("/state", GetState);

        return app;
    }

    /// <summary>
    /// Snapshot complet au chargement. Ensuite, seuls les deltas passent par SignalR :
    /// le client ne fait pas de polling (docs/03-architecture.md).
    /// </summary>
    private static async Task<IResult> GetState(
        RoomOsDbContext db,
        StateStore state,
        MqttLightService mqtt,
        TimeProvider time,
        CancellationToken ct)
    {
        var room = await db.Rooms.AsNoTracking().FirstOrDefaultAsync(ct);

        if (room is null)
        {
            return Results.Problem("Aucune pièce en base. Le seed a-t-il tourné ?");
        }

        var pcs = await db.Devices.AsNoTracking()
            .Where(d => d.Kind == DeviceKind.Pc && d.Enabled)
            .OrderBy(d => d.Id)
            .ToListAsync(ct);

        var outputsByPc = (await db.AudioOutputs.AsNoTracking()
                .OrderBy(o => o.SortOrder)
                .ToListAsync(ct))
            .GroupBy(o => o.PcDeviceId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var snapshot = new StateSnapshot(
            new RoomInfo(room.Id, room.Name),
            [.. pcs.Select(pc =>
            {
                var current = state.GetPc(pc.Id);
                return new PcSnapshot(
                    pc.Id,
                    pc.Name,
                    current.Online,
                    current.Uptime is { } uptime ? (long)uptime.TotalSeconds : null,
                    current.Telemetry);
            })],
            pcs.ToDictionary(pc => pc.Id, pc => BuildAudio(pc.Id, state, outputsByPc)),
            await LightEndpoints.BuildSnapshotsAsync(db, state, mqtt, ct),
            state.Music,
            await SceneEndpoints.ListAsync(db, ct),
            time.GetUtcNow());

        return Results.Ok(snapshot);
    }

    /// <summary>
    /// État audio d'un PC. Tant que l'agent n'a rien poussé — Core redémarré, PC
    /// éteint — on renvoie quand même les sorties déclarées en base, marquées
    /// débranchées. Sans ça la carte Audio serait vide et l'utilisateur ne saurait
    /// pas ce qui existe.
    /// </summary>
    private static AudioSnapshot BuildAudio(
        string pcId,
        StateStore state,
        IReadOnlyDictionary<string, List<AudioOutput>> outputsByPc)
    {
        if (state.GetAudio(pcId) is { } live)
        {
            return new AudioSnapshot(live.ActiveOutputId, live.Volume, live.Muted, live.Outputs);
        }

        var known = outputsByPc.TryGetValue(pcId, out var rows) ? rows : [];

        return new AudioSnapshot(
            null, 0, false,
            [.. known.Select(o => new AudioOutputInfo(o.Id, o.FriendlyName, false))]);
    }
}
