namespace RoomOS.Domain.Contracts;

/// <summary>
/// Une routine déclenche une scène à une heure donnée, certains jours.
/// </summary>
/// <remarks>
/// <para>
/// Elle n'exécute rien elle-même : elle appelle le moteur de scènes. Une automatisation
/// n'est donc jamais qu'un déclencheur posé devant quelque chose qui existe déjà, et
/// tout ce qui a été éprouvé sur les scènes vaut pour elles.
/// </para>
/// <para>
/// <paramref name="Days"/> compte sept booléens, de lundi à dimanche.
/// </para>
/// </remarks>
public sealed record RoutineInfo(
    string Id,
    string Name,
    string SceneId,
    string SceneName,
    /// <summary>Heure locale, au format <c>HH:mm</c>.</summary>
    string Time,
    IReadOnlyList<bool> Days,
    bool Enabled,
    /// <summary>Dernier déclenchement, heure locale. Vide si jamais déclenchée.</summary>
    string? LastFired);

public sealed record RoutineFired(string RoutineId, string SceneId, string RunId);
