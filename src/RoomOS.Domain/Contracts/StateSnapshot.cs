namespace RoomOS.Domain.Contracts;

/// <summary>
/// Réponse de <c>GET /api/state</c> : le snapshot complet au chargement.
/// Ensuite, seuls les deltas passent par SignalR.
/// </summary>
/// <remarks>
/// La forme grandit à chaque jalon. En M1 elle ne porte que la pièce et les PC ;
/// audio, lumières, musique et scènes s'ajoutent en M2 à M5.
/// </remarks>
public sealed record StateSnapshot(
    RoomInfo Room,
    IReadOnlyList<PcSnapshot> Pcs,
    DateTimeOffset ServerTime);

public sealed record RoomInfo(string Id, string Name);

public sealed record PcSnapshot(
    string Id,
    string Name,
    bool Online,
    long? UptimeSec,
    Telemetry? Telemetry);
