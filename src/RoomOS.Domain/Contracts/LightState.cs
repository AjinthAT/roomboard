namespace RoomOS.Domain.Contracts;

/// <summary>
/// Dernier état connu d'une lampe. En mémoire uniquement, comme le reste du runtime.
/// </summary>
/// <remarks>
/// <paramref name="Reachable"/> vient de la disponibilité publiée par Zigbee2MQTT :
/// une ampoule coupée à l'interrupteur mural est injoignable, ce qui n'est pas la
/// même chose qu'éteinte.
/// </remarks>
public sealed record LightState(
    bool On,
    int? Brightness,
    string? ColorHex,
    bool Reachable,
    DateTimeOffset UpdatedAt)
{
    public static LightState Unknown(DateTimeOffset at) => new(false, null, null, false, at);
}

public sealed record LightStateChanged(
    string Id, bool On, int? Brightness, string? ColorHex, bool Reachable);

public sealed record LightSnapshot(
    string Id,
    string Name,
    bool On,
    int? Brightness,
    string? ColorHex,
    bool Reachable,
    bool SupportsColor,
    bool SupportsBrightness);
