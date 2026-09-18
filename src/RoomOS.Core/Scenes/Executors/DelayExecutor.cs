using RoomOS.Domain.Contracts;

namespace RoomOS.Core.Scenes.Executors;

/// <summary>
/// Pause entre deux étapes. Sert à laisser un appareil réagir avant de lui parler à
/// nouveau — une bascule de sortie audio n'est pas instantanée côté Windows.
/// </summary>
public sealed class DelayExecutor : IStepExecutor
{
    public string Type => SceneStepTypes.Delay;

    public Task ExecuteAsync(SceneStep step, CancellationToken ct) =>
        Task.Delay(TimeSpan.FromMilliseconds(Math.Clamp(step.Ms ?? 0, 0, 10_000)), ct);
}
