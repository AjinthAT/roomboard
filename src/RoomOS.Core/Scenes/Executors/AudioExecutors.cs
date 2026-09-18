using Microsoft.AspNetCore.SignalR;
using RoomOS.Core.Agents;
using RoomOS.Core.Audio;
using RoomOS.Core.Hubs;
using RoomOS.Domain.Contracts;

namespace RoomOS.Core.Scenes.Executors;

public sealed class AudioSetOutputExecutor(
    IServiceScopeFactory scopeFactory,
    AgentRegistry registry,
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
    }
}

public sealed class AudioSetVolumeExecutor(
    AgentRegistry registry, IHubContext<AgentHub> hub) : IStepExecutor
{
    public string Type => SceneStepTypes.AudioSetVolume;

    public Task ExecuteAsync(SceneStep step, CancellationToken ct) =>
        AudioCommand.SendAsync(
            registry, hub, step.DeviceId, AgentProtocol.ToAgent.SetVolume,
            id => new SetVolumeCommand(id, step.Level ?? throw new InvalidOperationException("level manquant.")),
            ct);
}

public sealed class AudioSetMuteExecutor(
    AgentRegistry registry, IHubContext<AgentHub> hub) : IStepExecutor
{
    public string Type => SceneStepTypes.AudioSetMute;

    public Task ExecuteAsync(SceneStep step, CancellationToken ct) =>
        AudioCommand.SendAsync(
            registry, hub, step.DeviceId, AgentProtocol.ToAgent.SetMute,
            id => new SetMuteCommand(id, step.Muted ?? throw new InvalidOperationException("muted manquant.")),
            ct);
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
