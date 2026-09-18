using Microsoft.AspNetCore.SignalR;
using RoomOS.Core.Agents;
using RoomOS.Core.Audio;
using RoomOS.Core.Auth;
using RoomOS.Core.Hubs;
using RoomOS.Domain.Contracts;

namespace RoomOS.Core.Api;

public static class AudioEndpoints
{
    public sealed record SetOutputRequest(string OutputId);
    public sealed record SetVolumeRequest(int Level);
    public sealed record SetMuteRequest(bool Muted);

    public static IEndpointRouteBuilder MapAudioEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/audio")
            .RequireAuthorization(TokenAuthenticationHandler.ClientPolicy);

        group.MapPut("/{pcId}/output", SetOutput);
        group.MapPut("/{pcId}/volume", SetVolume);
        group.MapPut("/{pcId}/mute", SetMute);

        return app;
    }

    private static async Task<IResult> SetOutput(
        string pcId,
        SetOutputRequest request,
        AudioOutputResolver resolver,
        IHubContext<AgentHub> hub,
        AgentRegistry registry,
        CancellationToken ct)
    {
        var connectionId = registry.GetConnection(pcId);

        if (connectionId is null)
        {
            return Results.Conflict(new { message = $"L'agent de « {pcId} » n'est pas connecté." });
        }

        var windowsDeviceId = await resolver.GetWindowsDeviceIdAsync(pcId, request.OutputId, ct);

        if (windowsDeviceId is null)
        {
            // La sortie existe en base mais n'a jamais été vue par l'agent, ou l'identifiant
            // demandé est inconnu. Dans les deux cas, on ne devine pas.
            return Results.Conflict(new
            {
                message = $"La sortie « {request.OutputId} » n'est pas reconnue sur ce PC.",
            });
        }

        var commandId = NewCommandId();
        await hub.Clients.Client(connectionId).SendAsync(
            AgentProtocol.ToAgent.SetAudioOutput,
            new SetAudioOutputCommand(commandId, windowsDeviceId),
            ct);

        return Results.Accepted(value: new { commandId });
    }

    private static Task<IResult> SetVolume(
        string pcId,
        SetVolumeRequest request,
        IHubContext<AgentHub> hub,
        AgentRegistry registry,
        CancellationToken ct)
    {
        if (request.Level is < 0 or > 100)
        {
            return Task.FromResult(Results.BadRequest(new { message = "Le volume va de 0 à 100." }));
        }

        return SendAsync(
            pcId, registry, hub, AgentProtocol.ToAgent.SetVolume,
            id => new SetVolumeCommand(id, request.Level), ct);
    }

    private static Task<IResult> SetMute(
        string pcId,
        SetMuteRequest request,
        IHubContext<AgentHub> hub,
        AgentRegistry registry,
        CancellationToken ct) =>
        SendAsync(
            pcId, registry, hub, AgentProtocol.ToAgent.SetMute,
            id => new SetMuteCommand(id, request.Muted), ct);

    private static async Task<IResult> SendAsync(
        string pcId,
        AgentRegistry registry,
        IHubContext<AgentHub> hub,
        string method,
        Func<string, object> payload,
        CancellationToken ct)
    {
        var connectionId = registry.GetConnection(pcId);

        if (connectionId is null)
        {
            return Results.Conflict(new { message = $"L'agent de « {pcId} » n'est pas connecté." });
        }

        var commandId = NewCommandId();
        await hub.Clients.Client(connectionId).SendAsync(method, payload(commandId), ct);

        return Results.Accepted(value: new { commandId });
    }

    private static string NewCommandId() => Guid.NewGuid().ToString("n");
}
