namespace RoomOS.Domain.Contracts;

/// <summary>
/// Une étape de scène, telle qu'elle est écrite dans le DSL JSON.
/// </summary>
/// <remarks>
/// Un seul type porte les champs de tous les types d'étapes, plutôt qu'une hiérarchie
/// polymorphe. Le DSL est plat, fermé et court : une hiérarchie coûterait un
/// convertisseur JSON et trois fichiers pour aucun gain de lisibilité.
/// </remarks>
public sealed record SceneStep(
    string Type,
    string? DeviceId = null,
    string? OutputId = null,
    int? Level = null,
    bool? Muted = null,
    bool? On = null,
    int? Brightness = null,
    string? ColorHex = null,
    string? Uri = null,
    int? Ms = null,
    bool WaitForOnline = false,
    int? TimeoutSec = null,
    /// <summary>Température de blanc en mireds, exclusive de <c>ColorHex</c>.</summary>
    int? ColorTempMired = null,
    /// <summary>Effet Philips, pour les ampoules qui en exposent.</summary>
    string? Effect = null,
    /// <summary>Durée du fondu, en secondes. Une scène qui claque fait sursauter.</summary>
    double? TransitionSec = null);

/// <summary>Types d'étapes reconnus. Pas de condition, pas de boucle, pas de branche.</summary>
public static class SceneStepTypes
{
    public const string PcWake = "pc.wake";
    public const string PcShutdown = "pc.shutdown";
    public const string AudioSetOutput = "audio.setOutput";
    public const string AudioSetVolume = "audio.setVolume";
    public const string AudioSetMute = "audio.setMute";
    public const string LightSet = "light.set";
    public const string MusicTransfer = "music.transfer";
    public const string MusicPlay = "music.play";
    public const string MusicPause = "music.pause";
    public const string MusicSetVolume = "music.setVolume";
    public const string Delay = "delay";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        PcWake, PcShutdown, AudioSetOutput, AudioSetVolume, AudioSetMute,
        LightSet, MusicTransfer, MusicPlay, MusicPause, MusicSetVolume, Delay,
    };
}

public sealed record SceneDefinition(IReadOnlyList<SceneStep> Steps);

public enum SceneRunStatus
{
    Running,
    Completed,
    Failed,
    Cancelled,
}

public enum SceneStepStatus
{
    Completed,
    Failed,
    Skipped,
}

public sealed record SceneStepResult(int Index, string Type, SceneStepStatus Status, string? Message);

public sealed record SceneRun(
    string RunId,
    string SceneId,
    SceneRunStatus Status,
    IReadOnlyList<SceneStepResult> Steps,
    DateTimeOffset StartedAt);

/// <summary>
/// Une scène telle que l'UI la liste.
/// </summary>
/// <param name="Destructive">
/// Vrai si la scène éteint un PC. Calculé à partir de ses étapes, pas d'une liste
/// d'identifiants : une scène ajoutée plus tard qui éteint le poste sera marquée
/// sans qu'on ait à y penser.
/// </param>
public sealed record SceneInfo(string Id, string Name, string Icon, bool Destructive);

// Événements poussés sur le hub client pendant l'exécution.
public sealed record SceneStarted(string RunId, string SceneId);

public sealed record SceneStepCompleted(string RunId, int StepIndex, string Status, string? Message);

public sealed record SceneFinished(string RunId, string Status);
