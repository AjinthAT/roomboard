namespace RoomOS.Domain.Contracts;

/// <summary>
/// Seule abstraction « provider » du projet (ADR D7).
/// </summary>
/// <remarks>
/// Elle n'existe que parce que Spotify est la seule dépendance dont la politique
/// d'accès peut changer du jour au lendemain — c'est déjà arrivé en février 2026.
/// C'est une assurance, pas une invitation à ajouter d'autres implémentations.
/// </remarks>
public interface IMusicProvider
{
    Task<MusicState> GetStateAsync(CancellationToken ct);

    Task PlayAsync(string? uri, CancellationToken ct);

    Task PauseAsync(CancellationToken ct);

    Task NextAsync(CancellationToken ct);

    Task PreviousAsync(CancellationToken ct);

    Task SetVolumeAsync(int level, CancellationToken ct);
}

/// <summary>
/// Levée quand Spotify n'a aucun appareil actif. Distincte d'une panne : c'est
/// l'état normal quand l'application est fermée, et l'UI doit le dire plutôt que
/// d'afficher une erreur (docs/09-integrations.md).
/// </summary>
public sealed class NoActiveMusicDeviceException() : Exception("Aucun appareil Spotify actif.");

/// <summary>Levée quand l'intégration n'a jamais été autorisée.</summary>
public sealed class MusicNotLinkedException() : Exception("Spotify n'est pas encore autorisé.");
