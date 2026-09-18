namespace RoomOS.Domain.Contracts;

/// <summary>
/// Lecture en cours. Tous les champs sont nullables : Spotify peut être fermé,
/// sans appareil actif, ou en train de lire un contenu sans pochette.
/// </summary>
public sealed record NowPlaying(
    string? Title,
    string? Artist,
    string? AlbumArtUrl,
    bool IsPlaying,
    int? ProgressMs,
    int? DurationMs,
    string? DeviceName)
{
    /// <summary>Rien à afficher : Spotify fermé ou aucun appareil actif.</summary>
    public static readonly NowPlaying Nothing = new(null, null, null, false, null, null, null);

    public bool HasTrack => Title is not null;
}

/// <summary>
/// État de l'intégration musicale, distinct de la lecture elle-même : l'UI doit
/// pouvoir dire « pas encore autorisé » et « aucun appareil actif », qui ne sont
/// ni des erreurs ni des morceaux.
/// </summary>
public enum MusicLinkState
{
    /// <summary>Aucun jeton : le flux d'autorisation n'a jamais été mené.</summary>
    NotLinked,

    /// <summary>Autorisé, mais Spotify n'expose aucun appareil actif.</summary>
    NoActiveDevice,

    Ready,

    /// <summary>
    /// Spotify refuse l'accès : jeton révoqué, ou compte absent de la liste
    /// d'utilisateurs autorisés de l'application. Ce dernier cas est le piège le plus
    /// fréquent du mode développement (docs/09-integrations.md), et il est
    /// indiscernable d'une absence d'appareil si on ne le distingue pas.
    /// </summary>
    Denied,
}

public sealed record MusicState(MusicLinkState Link, NowPlaying NowPlaying);
