using Microsoft.EntityFrameworkCore;
using RoomOS.Core.Auth;
using RoomOS.Core.Data;
using RoomOS.Core.Data.Entities;
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
            time.GetUtcNow());

        return Results.Ok(snapshot);
    }
}
