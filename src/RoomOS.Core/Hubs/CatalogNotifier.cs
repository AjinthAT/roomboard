using Microsoft.AspNetCore.SignalR;
using RoomOS.Core.Api;
using RoomOS.Core.Data;
using RoomOS.Core.Routines;
using RoomOS.Domain.Contracts;

namespace RoomOS.Core.Hubs;

/// <summary>
/// Diffuse le catalogue de scènes et de routines après une modification.
///
/// Sans lui, l'iPad qui n'a pas fait l'édition garderait son ancienne liste jusqu'à
/// un rechargement : le panneau mural n'est jamais rechargé à la main.
/// </summary>
public sealed class CatalogNotifier(
    IHubContext<RoomHub> hub, RoutineScheduler scheduler, ILogger<CatalogNotifier> logger)
{
    public async Task PublishAsync(RoomOsDbContext db, CancellationToken ct)
    {
        try
        {
            var payload = new CatalogChanged(
                await SceneEndpoints.ListAsync(db, ct),
                await RoutineEndpoints.ListAsync(db, scheduler, ct));

            await hub.Clients.All.SendAsync(RoomProtocol.CatalogChanged, payload, ct);
        }
        catch (Exception ex)
        {
            // L'enregistrement est déjà validé : échouer la requête ici ferait croire
            // à l'utilisateur que sa scène n'a pas été sauvegardée.
            logger.LogWarning(ex, "Diffusion du catalogue impossible.");
        }
    }
}
