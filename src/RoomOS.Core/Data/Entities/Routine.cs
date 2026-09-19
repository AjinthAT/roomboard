namespace RoomOS.Core.Data.Entities;

public sealed class Routine
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public required string SceneId { get; set; }

    /// <summary>Minutes depuis minuit, heure locale. Plus simple à comparer qu'une chaîne.</summary>
    public int MinuteOfDay { get; set; }

    /// <summary>
    /// Sept caractères, de lundi à dimanche : <c>1</c> actif, <c>0</c> inactif.
    /// </summary>
    /// <remarks>
    /// Un masque texte plutôt qu'un entier : on le lit directement dans la base au
    /// moment où l'on cherche pourquoi une routine ne s'est pas déclenchée.
    /// </remarks>
    public required string Days { get; set; }

    public bool Enabled { get; set; }
    public int SortOrder { get; set; }
}
