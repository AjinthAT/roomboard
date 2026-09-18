using RoomOS.Domain.Contracts;

namespace RoomOS.Core.Scenes;

/// <summary>
/// Exécute un type d'étape de scène.
/// </summary>
/// <remarks>
/// <c>docs/08-scenes.md</c> l'autorise explicitement comme la <strong>seule</strong>
/// abstraction de ce type dans le projet, et pour une raison précise : le moteur doit
/// être testable sans PC, sans ampoule et sans Spotify.
/// </remarks>
public interface IStepExecutor
{
    /// <summary>Type d'étape pris en charge, parmi <see cref="SceneStepTypes"/>.</summary>
    string Type { get; }

    Task ExecuteAsync(SceneStep step, CancellationToken ct);
}
