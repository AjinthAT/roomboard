using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using RoomOS.Core.Agents;
using RoomOS.Core.Auth;
using RoomOS.Core.Data;
using RoomOS.Core.Data.Entities;
using RoomOS.Core.State;
using RoomOS.Domain.Contracts;

namespace RoomOS.Core.Hubs;

/// <summary>
/// Canal de l'agent Windows. L'agent se connecte en sortant : aucune règle de
/// pare-feu entrante n'est nécessaire sur le PC, et la présence de la connexion
/// suffit à savoir que le PC est allumé (docs/03-architecture.md).
/// </summary>
[Authorize(Policy = TokenAuthenticationHandler.AgentPolicy)]
public sealed class AgentHub(
    AgentRegistry registry,
    StateStore state,
    RoomOsDbContext db,
    ILogger<AgentHub> logger) : Hub
{
    /// <summary>
    /// Premier message de l'agent. Le PC annoncé doit exister en base : le jeton agent
    /// est partagé, il dit « c'est un agent », pas « c'est cet agent-là ». Sans ce
    /// contrôle, un agent pourrait s'enregistrer sous un identifiant arbitraire.
    /// </summary>
    public async Task Register(RegisterRequest request)
    {
        var known = await db.Devices.AsNoTracking()
            .AnyAsync(d => d.Id == request.PcId && d.Kind == DeviceKind.Pc && d.Enabled);

        if (!known)
        {
            logger.LogWarning("Enregistrement refusé : PC {PcId} inconnu.", request.PcId);
            throw new HubException($"PC « {request.PcId} » inconnu.");
        }

        registry.Register(request.PcId, Context.ConnectionId);
        state.SetOnline(request.PcId, TimeSpan.FromSeconds(request.UptimeSec));

        logger.LogInformation(
            "Agent {PcId} enregistré (version {Version}).", request.PcId, request.AgentVersion);
    }

    /// <summary>
    /// Le PC concerné vient de la connexion, jamais de la charge utile : sinon un
    /// agent authentifié pourrait écrire l'état d'un autre PC que le sien.
    /// </summary>
    public Task PushTelemetry(Telemetry telemetry)
    {
        var pcId = registry.GetPc(Context.ConnectionId);

        if (pcId is null)
        {
            throw new HubException("Register doit précéder PushTelemetry.");
        }

        state.SetTelemetry(pcId, telemetry);
        return Task.CompletedTask;
    }

    public Task Ack(CommandAck ack)
    {
        logger.LogInformation(
            "Commande {CommandId} acquittée : {Status} {Message}",
            ack.CommandId, ack.Status, ack.Message);

        return Task.CompletedTask;
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        var pcId = registry.Remove(Context.ConnectionId);

        if (pcId is not null)
        {
            // Déconnexion du hub = PC hors ligne. Pas de ping, pas d'heuristique.
            state.SetOffline(pcId);
            logger.LogInformation("Agent {PcId} déconnecté.", pcId);
        }

        return base.OnDisconnectedAsync(exception);
    }
}
