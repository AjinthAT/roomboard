using Microsoft.AspNetCore.SignalR;
using RoomOS.Core.Agents;
using RoomOS.Core.Audio;
using RoomOS.Core.Hubs;
using RoomOS.Core.State;
using RoomOS.Domain.Contracts;

namespace RoomOS.Core.Scenes.Executors;

public sealed class AudioSetOutputExecutor(
    IServiceScopeFactory scopeFactory,
    AgentRegistry registry,
    StateStore state,
    IHubContext<AgentHub> hub) : IStepExecutor
{
    public string Type => SceneStepTypes.AudioSetOutput;

    public async Task ExecuteAsync(SceneStep step, CancellationToken ct)
    {
        var pcId = step.DeviceId ?? throw new InvalidOperationException("deviceId manquant.");
        var outputId = step.OutputId ?? throw new InvalidOperationException("outputId manquant.");

        var connectionId = registry.GetConnection(pcId)
            ?? throw new InvalidOperationException($"L'agent de « {pcId} » n'est pas connecté.");

        await using var scope = scopeFactory.CreateAsyncScope();
        var resolver = scope.ServiceProvider.GetRequiredService<AudioOutputResolver>();

        var windowsDeviceId = await resolver.GetWindowsDeviceIdAsync(pcId, outputId, ct)
            ?? throw new InvalidOperationException($"Sortie « {outputId} » non reconnue.");

        await hub.Clients.Client(connectionId).SendAsync(
            AgentProtocol.ToAgent.SetAudioOutput,
            new SetAudioOutputCommand(Guid.NewGuid().ToString("n"), windowsDeviceId),
            ct);

        // L'agent republie l'état audio après bascule : on attend de le voir.
        await StateWaiter.UntilAsync(() => state.GetAudio(pcId)?.ActiveOutputId == outputId, ct);
    }
}

public sealed class AudioSetVolumeExecutor(
    AgentRegistry registry, StateStore state, IHubContext<AgentHub> hub) : IStepExecutor
{
    public string Type => SceneStepTypes.AudioSetVolume;

    public async Task ExecuteAsync(SceneStep step, CancellationToken ct)
    {
        var level = step.Level ?? throw new InvalidOperationException("level manquant.");

        await AudioCommand.SendAsync(
            registry, hub, step.DeviceId, AgentProtocol.ToAgent.SetVolume,
            id => new SetVolumeCommand(id, level), ct);

        // Windows arrondit le volume : un écart d'un point n'est pas un échec.
        await StateWaiter.UntilAsync(
            () => state.GetAudio(step.DeviceId!) is { } a && Math.Abs(a.Volume - level) <= 2, ct);
    }
}

public sealed class AudioSetMuteExecutor(
    AgentRegistry registry, StateStore state, IHubContext<AgentHub> hub) : IStepExecutor
{
    public string Type => SceneStepTypes.AudioSetMute;

    public async Task ExecuteAsync(SceneStep step, CancellationToken ct)
    {
        var muted = step.Muted ?? throw new InvalidOperationException("muted manquant.");

        await AudioCommand.SendAsync(
            registry, hub, step.DeviceId, AgentProtocol.ToAgent.SetMute,
            id => new SetMuteCommand(id, muted), ct);

        await StateWaiter.UntilAsync(() => state.GetAudio(step.DeviceId!)?.Muted == muted, ct);
    }
}

file static class AudioCommand
{
    public static async Task SendAsync(
        AgentRegistry registry,
        IHubContext<AgentHub> hub,
        string? pcId,
        string method,
        Func<string, object> payload,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pcId);

        var connectionId = registry.GetConnection(pcId)
            ?? throw new InvalidOperationException($"L'agent de « {pcId} » n'est pas connecté.");

        await hub.Clients.Client(connectionId).SendAsync(
            method, payload(Guid.NewGuid().ToString("n")), ct);
    }
}
