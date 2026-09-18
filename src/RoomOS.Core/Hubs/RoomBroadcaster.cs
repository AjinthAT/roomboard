using Microsoft.AspNetCore.SignalR;
using RoomOS.Core.Scenes;
using RoomOS.Core.State;
using RoomOS.Domain.Contracts;

namespace RoomOS.Core.Hubs;

/// <summary>
/// Relaie les mutations du <see cref="StateStore"/> vers les clients connectés.
/// Le store ne connaît pas SignalR : il émet des événements, ce service les diffuse.
/// </summary>
public sealed class RoomBroadcaster(
    StateStore state,
    SceneEngine scenes,
    IHubContext<RoomHub> hub,
    TimeProvider time,
    ILogger<RoomBroadcaster> logger) : IHostedService
{
    /// <summary>
    /// Plafond de diffusion de la télémétrie. L'agent publie toutes les 2 s, donc ce
    /// plafond n'est pas atteint en fonctionnement normal : c'est un garde-fou si
    /// l'intervalle de l'agent est abaissé (docs/05-api.md).
    /// </summary>
    private static readonly TimeSpan TelemetryFloor = TimeSpan.FromSeconds(1);

    private readonly Lock _gate = new();
    private DateTimeOffset _lastTelemetry = DateTimeOffset.MinValue;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        state.PcStateChanged += OnPcStateChanged;
        state.TelemetryUpdated += OnTelemetryUpdated;
        state.AudioStateChanged += OnAudioStateChanged;
        state.NowPlayingChanged += OnNowPlayingChanged;
        state.LightStateChanged += OnLightStateChanged;

        scenes.Started += p => Send(RoomProtocol.SceneStarted, p);
        scenes.StepCompleted += p => Send(RoomProtocol.SceneStepCompleted, p);
        scenes.Finished += p => Send(RoomProtocol.SceneFinished, p);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        state.PcStateChanged -= OnPcStateChanged;
        state.TelemetryUpdated -= OnTelemetryUpdated;
        state.AudioStateChanged -= OnAudioStateChanged;
        state.NowPlayingChanged -= OnNowPlayingChanged;
        state.LightStateChanged -= OnLightStateChanged;
        return Task.CompletedTask;
    }

    private void OnPcStateChanged(PcStateChanged payload) =>
        Send(RoomProtocol.PcStateChanged, payload);

    // L'état audio change rarement : pas de limitation de débit ici.
    private void OnAudioStateChanged(AudioStateChanged payload) =>
        Send(RoomProtocol.AudioStateChanged, payload);

    private void OnNowPlayingChanged(NowPlayingChanged payload) =>
        Send(RoomProtocol.NowPlayingChanged, payload);

    private void OnLightStateChanged(LightStateChanged payload) =>
        Send(RoomProtocol.LightStateChanged, payload);

    private void OnTelemetryUpdated(TelemetryUpdated payload)
    {
        var now = time.GetUtcNow();

        lock (_gate)
        {
            if (now - _lastTelemetry < TelemetryFloor)
            {
                return;
            }

            _lastTelemetry = now;
        }

        Send(RoomProtocol.TelemetryUpdated, payload);
    }

    private void Send(string method, object payload)
    {
        // Diffusion best-effort : un client lent ne doit pas bloquer le store.
        _ = hub.Clients.All.SendAsync(method, payload)
            .ContinueWith(
                t => logger.LogWarning(t.Exception, "Échec de diffusion de {Method}.", method),
                TaskContinuationOptions.OnlyOnFaulted);
    }
}
