using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using RoomOS.Core.Auth;

namespace RoomOS.Core.Hubs;

/// <summary>
/// Canal des clients. Le serveur pousse, le client n'appelle rien : pour agir,
/// il passe par REST (docs/05-api.md).
/// </summary>
[Authorize(Policy = TokenAuthenticationHandler.ClientPolicy)]
public sealed class RoomHub(ILogger<RoomHub> logger) : Hub
{
    public override Task OnConnectedAsync()
    {
        logger.LogInformation("Client {ConnectionId} connecté.", Context.ConnectionId);
        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        logger.LogInformation("Client {ConnectionId} déconnecté.", Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}
