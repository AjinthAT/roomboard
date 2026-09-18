using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using RoomOS.Core.Agents;
using RoomOS.Core.Auth;
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
    ILogger<AgentHub> logger) : Hub
{
    public Task Register(RegisterRequest request)
    {
        registry.Register(request.PcId, Context.ConnectionId);
        state.SetOnline(request.PcId, TimeSpan.FromSeconds(request.UptimeSec));

        logger.LogInformation(
            "Agent {PcId} enregistré (version {Version}).", request.PcId, request.AgentVersion);

        return Task.CompletedTask;
    }

    public Task PushTelemetry(string pcId, Telemetry telemetry)
    {
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
