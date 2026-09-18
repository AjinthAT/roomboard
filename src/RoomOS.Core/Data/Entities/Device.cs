namespace RoomOS.Core.Data.Entities;

public enum DeviceKind
{
    Pc,
    Light,
}

/// <summary>
/// Un appareil piloté. La configuration spécifique au type vit dans
/// <see cref="ConfigJson"/> (voir docs/04-domaine.md).
/// </summary>
public sealed class Device
{
    public required string Id { get; set; }
    public required string RoomId { get; set; }
    public required DeviceKind Kind { get; set; }
    public required string Name { get; set; }
    public bool Enabled { get; set; } = true;
    public string ConfigJson { get; set; } = "{}";

    public Room? Room { get; set; }
}

/// <summary>Contenu de <see cref="Device.ConfigJson"/> quand le type est <c>Pc</c>.</summary>
/// <remarks>
/// Pas de jeton ici : l'agent s'authentifie avec <c>ROOMOS__AgentToken</c>, une
/// variable d'environnement. Aucun secret ne descend en base.
/// </remarks>
public sealed record PcConfig(string Mac, string Ip, string Broadcast);
