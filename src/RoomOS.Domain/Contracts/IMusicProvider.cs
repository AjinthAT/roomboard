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

    /// <summary>Appareils Spotify visibles par le compte.</summary>
    Task<IReadOnlyList<MusicDevice>> GetDevicesAsync(CancellationToken ct);

    /// <summary>
    /// Transfère la lecture vers l'appareil dont le nom contient <paramref name="hint"/>.
    /// </summary>
    /// <remarks>
    /// Les endpoints Player s'appliquent à l'appareil actif du compte, pas à une
    /// machine choisie. Sans transfert, une scène lancerait la musique là où elle
    /// jouait la dernière fois — un téléphone, par exemple (ADR D11).
    /// </remarks>
    Task TransferToAsync(string hint, CancellationToken ct);

    /// <summary>Transfère la lecture vers un appareil désigné par son identifiant.</summary>
    Task TransferToDeviceAsync(string deviceId, CancellationToken ct);

    Task SetShuffleAsync(bool enabled, CancellationToken ct);

    /// <summary>« off », « track » ou « context ».</summary>
    Task SetRepeatAsync(string mode, CancellationToken ct);

    Task SeekAsync(int positionMs, CancellationToken ct);

    /// <summary>File d'attente. Le premier élément est le morceau suivant.</summary>
    Task<IReadOnlyList<MusicTrack>> GetQueueAsync(CancellationToken ct);

    Task QueueAsync(string uri, CancellationToken ct);

    /// <summary>
    /// Recherche dans le catalogue. Plafonnée à dix résultats par Spotify depuis
    /// février 2026 (docs/09-integrations.md).
    /// </summary>
    Task<IReadOnlyList<MusicTrack>> SearchAsync(string query, CancellationToken ct);
}

public sealed record MusicTrack(string Uri, string Title, string Artist, string? AlbumArtUrl);

public sealed record MusicDevice(string Id, string Name, bool IsActive, string Type);

/// <summary>
/// Levée quand Spotify n'a aucun appareil actif. Distincte d'une panne : c'est
/// l'état normal quand l'application est fermée, et l'UI doit le dire plutôt que
/// d'afficher une erreur (docs/09-integrations.md).
/// </summary>
public sealed class NoActiveMusicDeviceException() : Exception("Aucun appareil Spotify actif.");

/// <summary>Levée quand l'intégration n'a jamais été autorisée.</summary>
public sealed class MusicNotLinkedException() : Exception("Spotify n'est pas encore autorisé.");
