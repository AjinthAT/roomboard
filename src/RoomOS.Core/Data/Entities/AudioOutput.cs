namespace RoomOS.Core.Data.Entities;

/// <summary>
/// Une sortie audio d'un PC. Source de vérité unique : une sortie n'est pas un
/// <see cref="Device"/> (docs/04-domaine.md).
/// </summary>
/// <remarks>
/// <see cref="WindowsDeviceId"/> est la clé de correspondance, <see cref="MatchHint"/>
/// le filet si cet identifiant change — réinstallation de pilote, changement de port
/// USB — et <see cref="FriendlyName"/> ce que voit l'utilisateur. On n'identifie
/// jamais une sortie par son seul nom Windows.
/// </remarks>
public sealed class AudioOutput
{
    public required string Id { get; set; }
    public required string PcDeviceId { get; set; }

    /// <summary>Vide tant que l'agent n'a pas énuméré les sorties du PC.</summary>
    public string? WindowsDeviceId { get; set; }

    public required string MatchHint { get; set; }
    public required string FriendlyName { get; set; }
    public int SortOrder { get; set; }
}
