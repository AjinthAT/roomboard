using System.Collections.Concurrent;
using RoomOS.Domain.Contracts;

namespace RoomOS.Core.State;

/// <summary>
/// Dernier état connu de chaque appareil. En mémoire uniquement : ce qui change
/// toutes les secondes n'a aucune valeur historique et ne va pas en base (ADR D6).
/// </summary>
public sealed class StateStore
{
    private readonly ConcurrentDictionary<string, PcState> _pcs = new();
    private readonly ConcurrentDictionary<string, AudioState> _audio = new();
    private readonly TimeProvider _time;

    public StateStore(TimeProvider time) => _time = time;

    public event Action<PcStateChanged>? PcStateChanged;
    public event Action<TelemetryUpdated>? TelemetryUpdated;
    public event Action<AudioStateChanged>? AudioStateChanged;
    public event Action<NowPlayingChanged>? NowPlayingChanged;
    public event Action<LightStateChanged>? LightStateChanged;

    public IReadOnlyDictionary<string, PcState> Pcs => _pcs;
    public IReadOnlyDictionary<string, AudioState> Audio => _audio;

    public PcState GetPc(string pcId) =>
        _pcs.TryGetValue(pcId, out var state) ? state : PcState.Offline(_time.GetUtcNow());

    /// <summary>Un PC passe en ligne : l'agent vient de s'enregistrer.</summary>
    public void SetOnline(string pcId, TimeSpan uptime)
    {
        var state = new PcState(true, uptime, GetPc(pcId).Telemetry, _time.GetUtcNow());
        _pcs[pcId] = state;
        PcStateChanged?.Invoke(new PcStateChanged(pcId, true, (long)uptime.TotalSeconds));
    }

    /// <summary>
    /// Un PC passe hors ligne : la connexion du hub agent est tombée. C'est le seul
    /// signal utilisé, il n'y a pas de ping (docs/05-api.md).
    /// </summary>
    public void SetOffline(string pcId)
    {
        _pcs[pcId] = PcState.Offline(_time.GetUtcNow());

        // L'état audio d'un PC éteint n'a pas de sens : on ne le garde pas plus que
        // sa télémétrie. Les sorties restent listées, mais aucune n'est active.
        if (_audio.TryGetValue(pcId, out var audio))
        {
            var cleared = audio with
            {
                ActiveOutputId = null,
                Outputs = [.. audio.Outputs.Select(o => o with { Connected = false })],
            };
            _audio[pcId] = cleared;
            AudioStateChanged?.Invoke(new AudioStateChanged(
                pcId, null, cleared.Volume, cleared.Muted, cleared.Outputs));
        }

        PcStateChanged?.Invoke(new PcStateChanged(pcId, false, null));
    }

    private readonly ConcurrentDictionary<string, LightState> _lights = new();

    public IReadOnlyDictionary<string, LightState> Lights => _lights;

    public LightState? GetLight(string deviceId) =>
        _lights.TryGetValue(deviceId, out var light) ? light : null;

    public void SetLight(string deviceId, LightState light)
    {
        var previous = GetLight(deviceId);
        _lights[deviceId] = light;

        // Zigbee2MQTT republie l'état complet à chaque événement, y compris quand
        // rien n'a bougé. Rediffuser à l'identique réveillerait l'iPad pour rien.
        if (previous is not null
            && previous.On == light.On
            && previous.Brightness == light.Brightness
            && previous.ColorHex == light.ColorHex
            && previous.Reachable == light.Reachable)
        {
            return;
        }

        LightStateChanged?.Invoke(new LightStateChanged(
            deviceId, light.On, light.Brightness, light.ColorHex, light.Reachable));
    }

    private MusicState _music = new(MusicLinkState.NotLinked, NowPlaying.Nothing);

    public MusicState Music => _music;

    public void SetMusic(MusicState music)
    {
        var previous = _music;
        _music = music;

        // La progression du morceau avance à chaque sondage : la diffuser ferait un
        // message toutes les 3 s pour une valeur que l'UI n'affiche pas en continu.
        // Seul un vrai changement compte.
        if (previous.Link == music.Link && SameTrack(previous.NowPlaying, music.NowPlaying))
        {
            return;
        }

        NowPlayingChanged?.Invoke(new NowPlayingChanged(music.Link, music.NowPlaying));
    }

    private static bool SameTrack(NowPlaying a, NowPlaying b) =>
        a.Title == b.Title && a.Artist == b.Artist && a.IsPlaying == b.IsPlaying
        && a.AlbumArtUrl == b.AlbumArtUrl && a.DeviceName == b.DeviceName;

    public AudioState? GetAudio(string pcId) =>
        _audio.TryGetValue(pcId, out var state) ? state : null;

    public void SetAudio(string pcId, AudioState state)
    {
        var previous = GetAudio(pcId);
        _audio[pcId] = state;

        // L'agent republie toutes les 10 s même sans changement, en filet. Rediffuser
        // à l'identique réveillerait les clients pour rien.
        //
        // La comparaison est explicite : l'opérateur == d'un record compare
        // IReadOnlyList par référence, et chaque publication porte une nouvelle liste.
        if (!HasChanged(previous, state))
        {
            return;
        }

        AudioStateChanged?.Invoke(
            new AudioStateChanged(pcId, state.ActiveOutputId, state.Volume, state.Muted, state.Outputs));
    }

    private static bool HasChanged(AudioState? previous, AudioState current)
    {
        if (previous is null)
        {
            return true;
        }

        return previous.ActiveOutputId != current.ActiveOutputId
            || previous.Volume != current.Volume
            || previous.Muted != current.Muted
            || !previous.Outputs.SequenceEqual(current.Outputs);
    }

    public void SetTelemetry(string pcId, Telemetry telemetry)
    {
        var previous = GetPc(pcId);

        // Une télémétrie en vol peut arriver après la déconnexion de l'agent. Sans ce
        // garde-fou, elle réinjecte des mesures dans l'état d'un PC déclaré hors ligne,
        // et le prochain GET /api/state affiche des valeurs mortes comme si elles
        // étaient vivantes.
        if (!previous.Online)
        {
            return;
        }

        _pcs[pcId] = previous with { Telemetry = telemetry, UpdatedAt = _time.GetUtcNow() };
        TelemetryUpdated?.Invoke(new TelemetryUpdated(pcId, telemetry));
    }
}
