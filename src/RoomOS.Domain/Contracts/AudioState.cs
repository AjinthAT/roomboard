namespace RoomOS.Domain.Contracts;

/// <summary>
/// État audio d'un PC, en mémoire uniquement.
/// </summary>
/// <remarks>
/// <paramref name="ActiveOutputId"/> est nullable, et c'est un cas nominal : l'agent
/// pousse un identifiant Windows, que le Core mappe vers une sortie connue. Si la
/// sortie active est un périphérique qu'on n'a pas enregistré — écran HDMI, casque
/// Bluetooth branché à la volée — le mapping ne donne rien. L'UI affiche alors
/// « sortie inconnue » plutôt que d'en désigner une au hasard (docs/04-domaine.md).
/// </remarks>
public sealed record AudioState(
    string? ActiveOutputId,
    int Volume,
    bool Muted,
    IReadOnlyList<AudioOutputInfo> Outputs,
    DateTimeOffset UpdatedAt);

/// <summary>Une sortie telle que l'UI la voit.</summary>
public sealed record AudioOutputInfo(string Id, string Name, bool Connected);

/// <summary>Une sortie telle que l'agent l'a énumérée sur Windows.</summary>
public sealed record WindowsAudioOutput(string WindowsDeviceId, string Name);
