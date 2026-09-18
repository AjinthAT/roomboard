using System.Collections.Concurrent;

namespace RoomOS.Core.Agents;

/// <summary>
/// Associe un PC à la connexion SignalR de son agent. La présence d'une entrée
/// ici <em>est</em> l'information « PC en ligne ».
/// </summary>
public sealed class AgentRegistry
{
    private readonly ConcurrentDictionary<string, string> _connectionByPc = new();
    private readonly ConcurrentDictionary<string, string> _pcByConnection = new();

    public void Register(string pcId, string connectionId)
    {
        _connectionByPc[pcId] = connectionId;
        _pcByConnection[connectionId] = pcId;
    }

    /// <summary>Retire une connexion et renvoie le PC concerné, s'il était enregistré.</summary>
    public string? Remove(string connectionId)
    {
        if (!_pcByConnection.TryRemove(connectionId, out var pcId))
        {
            return null;
        }

        // Ne pas retirer une connexion plus récente : l'agent a pu se reconnecter
        // avant que la déconnexion précédente ne soit traitée.
        _connectionByPc.TryGetValue(pcId, out var current);
        if (current == connectionId)
        {
            _connectionByPc.TryRemove(pcId, out _);
        }

        return pcId;
    }

    public string? GetConnection(string pcId) =>
        _connectionByPc.TryGetValue(pcId, out var connectionId) ? connectionId : null;

    public bool IsOnline(string pcId) => _connectionByPc.ContainsKey(pcId);
}
