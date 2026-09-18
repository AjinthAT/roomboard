using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using RoomOS.Core.Auth;

namespace RoomOS.Core.Hubs;

/// <summary>
/// Canal des clients. Le serveur pousse, le client n'appelle rien : pour agir,
/// il passe par REST (docs/05-api.md).
/// </summary>
[Authorize(Policy = TokenAuthenticationHandler.ClientPolicy)]
public sealed class RoomHub : Hub;
