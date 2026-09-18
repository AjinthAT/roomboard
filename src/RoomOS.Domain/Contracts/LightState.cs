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
    /// <summary>
    /// Vrai dès que Zigbee2MQTT a parlé de cette lampe au moins une fois.
    /// </summary>
    /// <remarks>
    /// Distinct de <c>Reachable</c>, et la nuance compte à l'écran : une lampe jamais
    /// vue n'est pas appairée, une lampe vue puis muette est hors de portée ou coupée
    /// au mur. Confondre les deux envoie chercher une panne là où il n'y a qu'une
    /// installation inachevée.
    /// </remarks>
    bool Paired,
    bool SupportsColor,
    bool SupportsBrightness);
