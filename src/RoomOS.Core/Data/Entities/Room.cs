namespace RoomOS.Core.Data.Entities;

/// <summary>
/// Une pièce. Une seule ligne en V1 (<c>bedroom</c>) : la table existe pour ne pas
/// avoir à migrer plus tard, l'UI multi-pièces n'existe pas (ADR D8).
/// </summary>
public sealed class Room
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public int SortOrder { get; set; }

    public List<Device> Devices { get; set; } = [];
}
