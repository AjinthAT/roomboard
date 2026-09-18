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
}

public sealed record PcStateChanged(string Id, bool Online, long? UptimeSec);

public sealed record TelemetryUpdated(string Id, Telemetry Telemetry);

public sealed record NowPlayingChanged(MusicLinkState Link, NowPlaying NowPlaying);

public sealed record AudioStateChanged(
    string PcId,
    string? ActiveOutputId,
    int Volume,
    bool Muted,
    IReadOnlyList<AudioOutputInfo> Outputs);
