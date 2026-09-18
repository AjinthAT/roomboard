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
    /// Sorties audio du PC. Les indices Windows n'étant connus qu'après énumération
    /// par l'agent, seul l'indice de correspondance est configuré ici.
    /// </summary>
    public List<AudioOutputOptions> AudioOutputs { get; set; } =
    [
        new() { Id = "jbl", Name = "JBL", MatchHint = "JBL" },
        new() { Id = "headset", Name = "Casque", MatchHint = "Casque" },
    ];
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
