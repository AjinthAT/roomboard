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
    private readonly TimeProvider _time;

    public StateStore(TimeProvider time) => _time = time;

    public event Action<PcStateChanged>? PcStateChanged;
    public event Action<TelemetryUpdated>? TelemetryUpdated;

    public IReadOnlyDictionary<string, PcState> Pcs => _pcs;

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
        PcStateChanged?.Invoke(new PcStateChanged(pcId, false, null));
    }

    public void SetTelemetry(string pcId, Telemetry telemetry)
    {
        var previous = GetPc(pcId);
        _pcs[pcId] = previous with { Telemetry = telemetry, UpdatedAt = _time.GetUtcNow() };
        TelemetryUpdated?.Invoke(new TelemetryUpdated(pcId, telemetry));
    }
}
