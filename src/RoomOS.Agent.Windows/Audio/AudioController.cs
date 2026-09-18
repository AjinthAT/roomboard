using NAudio.CoreAudioApi;
using RoomOS.Domain.Contracts;

namespace RoomOS.Agent.Windows.Audio;

/// <summary>
/// Énumère les sorties audio, lit et modifie le volume, et bascule la sortie par défaut.
/// </summary>
/// <remarks>
/// L'énumération et le volume passent par les API publiques WASAPI via NAudio. Seule
/// la bascule de sortie par défaut exige l'interface non documentée
/// <see cref="PolicyConfig"/>.
/// </remarks>
public sealed class AudioController(ILogger<AudioController> logger) : IDisposable
{
    private readonly MMDeviceEnumerator _enumerator = new();

    public AgentAudioState Read()
    {
        var outputs = new List<WindowsAudioOutput>();
        string? activeId = null;
        var volume = 0;
        var muted = false;

        foreach (var device in _enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
        {
            using (device)
            {
                outputs.Add(new WindowsAudioOutput(device.ID, device.FriendlyName));
            }
        }

        try
        {
            using var active = _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Console);
            activeId = active.ID;
            volume = (int)Math.Round(active.AudioEndpointVolume.MasterVolumeLevelScalar * 100);
            muted = active.AudioEndpointVolume.Mute;
        }
        catch (Exception ex)
        {
            // Aucune sortie par défaut : possible juste après un débranchement.
            logger.LogWarning(ex, "Pas de sortie par défaut lisible.");
        }

        return new AgentAudioState(activeId, volume, muted, outputs);
    }

    public bool TrySetOutput(string windowsDeviceId)
    {
        if (!PolicyConfig.TrySetDefaultOutput(windowsDeviceId, out var error))
        {
            logger.LogError("Bascule vers {DeviceId} refusée : {Error}", windowsDeviceId, error);
            return false;
        }

        logger.LogInformation("Sortie par défaut basculée vers {DeviceId}.", windowsDeviceId);
        return true;
    }

    public bool TrySetVolume(int level)
    {
        var clamped = Math.Clamp(level, 0, 100);

        return WithDefaultEndpoint(device =>
            device.AudioEndpointVolume.MasterVolumeLevelScalar = clamped / 100f);
    }

    public bool TrySetMute(bool muted) =>
        WithDefaultEndpoint(device => device.AudioEndpointVolume.Mute = muted);

    private bool WithDefaultEndpoint(Action<MMDevice> action)
    {
        try
        {
            using var device = _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Console);
            action(device);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Action impossible sur la sortie par défaut.");
            return false;
        }
    }

    public void Dispose() => _enumerator.Dispose();
}
