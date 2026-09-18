using Microsoft.Extensions.Options;
using RoomOS.Core.Configuration;
using RoomOS.Domain.Contracts;

namespace RoomOS.Core.Scenes.Executors;

/// <summary>
/// Transfère la lecture vers le PC avant toute commande musicale de scène (ADR D11).
/// </summary>
/// <remarks>
/// Sans cette étape, une scène piloterait l'appareil actif du compte, qui peut être
/// un téléphone. Le critère de réussite du projet — appuyer sur « Gaming » et que
/// tout soit prêt — ne tient pas autrement.
/// </remarks>
public sealed class MusicTransferExecutor(
    IServiceScopeFactory scopeFactory, IOptions<RoomOsOptions> options) : IStepExecutor
{
    public string Type => SceneStepTypes.MusicTransfer;

    public Task ExecuteAsync(SceneStep step, CancellationToken ct) =>
        MusicCommand.RunAsync(
            scopeFactory,
            music => music.TransferToAsync(options.Value.Spotify.PcDeviceHint, ct),
            ct);
}

public sealed class MusicPlayExecutor(IServiceScopeFactory scopeFactory) : IStepExecutor
{
    public string Type => SceneStepTypes.MusicPlay;

    public Task ExecuteAsync(SceneStep step, CancellationToken ct) =>
        MusicCommand.RunAsync(scopeFactory, music => music.PlayAsync(step.Uri, ct), ct);
}

public sealed class MusicPauseExecutor(IServiceScopeFactory scopeFactory) : IStepExecutor
{
    public string Type => SceneStepTypes.MusicPause;

    public Task ExecuteAsync(SceneStep step, CancellationToken ct) =>
        MusicCommand.RunAsync(scopeFactory, music => music.PauseAsync(ct), ct);
}

public sealed class MusicSetVolumeExecutor(IServiceScopeFactory scopeFactory) : IStepExecutor
{
    public string Type => SceneStepTypes.MusicSetVolume;

    public Task ExecuteAsync(SceneStep step, CancellationToken ct) =>
        MusicCommand.RunAsync(
            scopeFactory,
            music => music.SetVolumeAsync(
                step.Level ?? throw new InvalidOperationException("level manquant."), ct),
            ct);
}

file static class MusicCommand
{
    public static async Task RunAsync(
        IServiceScopeFactory scopeFactory,
        Func<IMusicProvider, Task> action,
        CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var music = scope.ServiceProvider.GetRequiredService<IMusicProvider>();

        await action(music);
    }
}
