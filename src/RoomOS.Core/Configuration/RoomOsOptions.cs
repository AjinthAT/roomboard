namespace RoomOS.Core.Configuration;

/// <summary>
/// Configuration du Core, liée depuis les variables d'environnement <c>ROOMOS__*</c>.
/// Aucun secret n'est stocké en base (docs/11-conventions.md).
/// </summary>
public sealed class RoomOsOptions
{
    public const string SectionName = "ROOMOS";

    /// <summary>Jeton du front, saisi une fois dans l'UI puis gardé en localStorage.</summary>
    public string ApiToken { get; set; } = string.Empty;

    /// <summary>Jeton de l'agent Windows. Distinct de celui du front.</summary>
    public string AgentToken { get; set; } = string.Empty;

    public string DatabasePath { get; set; } = "roomos.db";

    public PcOptions Pc { get; set; } = new();

    /// <summary>
    /// Sorties audio du PC. Les identifiants Windows n'étant connus qu'après
    /// énumération par l'agent, seul l'indice de correspondance est configuré ici.
    /// </summary>
    /// <remarks>
    /// Liste vide par défaut, à dessein : le binder de configuration .NET <em>ajoute</em>
    /// aux collections existantes au lieu de les remplacer. Des valeurs par défaut ici
    /// se cumuleraient avec celles de l'environnement. Le repli est dans le seeder.
    /// </remarks>
    public List<AudioOutputOptions> AudioOutputs { get; set; } = [];

    public SpotifyOptions Spotify { get; set; } = new();

    public MqttOptions Mqtt { get; set; } = new();

    /// <summary>
    /// Lampes déclarées. Vide par défaut, pour la même raison que les sorties audio :
    /// le binder de configuration ajoute aux collections au lieu de les remplacer.
    /// </summary>
    public List<LightOptions> Lights { get; set; } = [];
}

public sealed class MqttOptions
{
    /// <summary>
    /// Le Core tourne en réseau hôte pour le Wake-on-LAN, il ne résout donc pas les
    /// noms de services Docker : Mosquitto se joint par la boucle locale.
    /// </summary>
    public string Host { get; set; } = "127.0.0.1";

    public int Port { get; set; } = 1883;

    public string BaseTopic { get; set; } = "zigbee2mqtt";
}

public sealed class LightOptions
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Z2mFriendlyName { get; set; } = string.Empty;
    public bool SupportsColor { get; set; } = true;
    public bool SupportsBrightness { get; set; } = true;
}

public sealed class SpotifyOptions
{
    /// <summary>Public par nature : le flux PKCE n'utilise pas de secret client.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Spotify impose HTTPS hors adresses de bouclage littérales. Ni une adresse LAN,
    /// ni « localhost » ne sont acceptés (docs/09-integrations.md).
    /// </summary>
    public string RedirectUri { get; set; } = "http://127.0.0.1:8080/api/music/callback";

    /// <summary>
    /// Fragment du nom de l'appareil Spotify correspondant au PC. Spotify nomme
    /// généralement un appareil d'après la machine. <c>GET /api/music/devices</c>
    /// liste les noms visibles.
    /// </summary>
    public string PcDeviceHint { get; set; } = string.Empty;

    /// <summary>
    /// Playlists mises en avant dans l'UI. Vide par défaut, comme les autres listes :
    /// le binder de configuration ajoute aux collections au lieu de les remplacer.
    /// </summary>
    public List<PlaylistOptions> Playlists { get; set; } = [];

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ClientId);
}

public sealed class PlaylistOptions
{
    public string Name { get; set; } = string.Empty;

    /// <summary>URI Spotify, de la forme <c>spotify:playlist:…</c>.</summary>
    public string Uri { get; set; } = string.Empty;
}

public sealed class AudioOutputOptions
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    /// <summary>Fragment cherché dans le nom Windows du périphérique, sans casse.</summary>
    public string MatchHint { get; set; } = string.Empty;
}

public sealed class PcOptions
{
    public string Id { get; set; } = "gaming-pc";
    public string Name { get; set; } = "PC";
    public string Mac { get; set; } = string.Empty;
    public string Ip { get; set; } = string.Empty;
    public string Broadcast { get; set; } = "255.255.255.255";
}
