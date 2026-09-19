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
    DateTimeOffset UpdatedAt,
    /// <summary>Température de blanc en mireds. Plus la valeur est basse, plus c'est froid.</summary>
    int? ColorTempMired = null,
    /// <summary>Qualité du lien radio, 0 à 255. Diagnostic, pas une commande.</summary>
    int? LinkQuality = null,
    /// <summary>Comportement au rétablissement du courant : off, on, toggle, previous.</summary>
    string? PowerOnBehavior = null)
{
    public static LightState Unknown(DateTimeOffset at) => new(false, null, null, false, at);
}

public sealed record LightStateChanged(
    string Id,
    bool On,
    int? Brightness,
    string? ColorHex,
    bool Reachable,
    int? ColorTempMired = null,
    int? LinkQuality = null,
    string? PowerOnBehavior = null);

/// <summary>
/// Ce qu'une lampe sait faire, déduit de l'inventaire publié par Zigbee2MQTT.
/// </summary>
/// <remarks>
/// Déduites plutôt que déclarées en configuration : l'interface s'adapte à chaque
/// ampoule appairée, sans qu'on ait à décrire son modèle quelque part.
/// </remarks>
public sealed record LightCapabilities(
    bool Brightness,
    bool Color,
    bool ColorTemp,
    int? ColorTempMin,
    int? ColorTempMax,
    IReadOnlyList<string> Effects,
    IReadOnlyList<string> PowerOnBehaviours)
{
    public static readonly LightCapabilities None =
        new(false, false, false, null, null, [], []);
}

public sealed record LightSnapshot(
    string Id,
    string Name,
    bool On,
    int? Brightness,
    string? ColorHex,
    bool Reachable,
    /// <summary>
    /// Vrai dès que Zigbee2MQTT liste cette lampe dans son inventaire.
    /// </summary>
    /// <remarks>
    /// Distinct de <c>Reachable</c>, et la nuance compte à l'écran : une lampe jamais
    /// appairée n'est pas une lampe en panne. La source est l'inventaire, pas la
    /// réception d'un message : sinon une lampe immobile depuis le démarrage du Core
    /// paraîtrait absente, et une lampe partie paraîtrait présente.
    /// </remarks>
    bool Paired,
    int? ColorTempMired,
    int? LinkQuality,
    string? PowerOnBehavior,
    LightCapabilities Capabilities);
