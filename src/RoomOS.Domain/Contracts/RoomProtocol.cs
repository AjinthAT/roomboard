namespace RoomOS.Domain.Contracts;

/// <summary>
/// Événements poussés par le Core vers les clients. Le client n'appelle aucune
/// méthode du hub : il agit par REST (voir docs/05-api.md).
/// </summary>
public static class RoomProtocol
{
    public const string PcStateChanged = "PcStateChanged";
    public const string TelemetryUpdated = "TelemetryUpdated";
    public const string AudioStateChanged = "AudioStateChanged";
    public const string NowPlayingChanged = "NowPlayingChanged";
    public const string LightStateChanged = "LightStateChanged";
    public const string SceneStarted = "SceneStarted";
    public const string SceneStepCompleted = "SceneStepCompleted";
    public const string SceneFinished = "SceneFinished";
    public const string CatalogChanged = "CatalogChanged";
}

public sealed record PcStateChanged(string Id, bool Online, long? UptimeSec);

/// <summary>
/// Le catalogue de scènes et de routines a changé. Il est diffusé en entier : il fait
/// une dizaine de lignes, et un delta ici coûterait plus cher à maintenir qu'à envoyer.
/// </summary>
public sealed record CatalogChanged(
    IReadOnlyList<SceneInfo> Scenes,
    IReadOnlyList<RoutineInfo> Routines);

public sealed record TelemetryUpdated(string Id, Telemetry Telemetry);

public sealed record NowPlayingChanged(MusicLinkState Link, NowPlaying NowPlaying);

public sealed record AudioStateChanged(
    string PcId,
    string? ActiveOutputId,
    int Volume,
    bool Muted,
    IReadOnlyList<AudioOutputInfo> Outputs);
