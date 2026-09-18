namespace RoomOS.Core.Data.Entities;

public sealed class Scene
{
    public required string Id { get; set; }
    public required string RoomId { get; set; }
    public required string Name { get; set; }

    /// <summary>Nom d'icône lucide, rendu côté front.</summary>
    public required string Icon { get; set; }

    /// <summary>DSL décrit dans docs/08-scenes.md.</summary>
    public required string StepsJson { get; set; }

    public int SortOrder { get; set; }
}
