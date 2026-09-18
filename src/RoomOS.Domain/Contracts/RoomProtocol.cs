namespace RoomOS.Domain.Contracts;

/// <summary>
/// Événements poussés par le Core vers les clients. Le client n'appelle aucune
/// méthode du hub : il agit par REST (voir docs/05-api.md).
/// </summary>
public static class RoomProtocol
{
    public const string PcStateChanged = "PcStateChanged";
    public const string TelemetryUpdated = "TelemetryUpdated";
}

public sealed record PcStateChanged(string Id, bool Online, long? UptimeSec);

public sealed record TelemetryUpdated(string Id, Telemetry Telemetry);
