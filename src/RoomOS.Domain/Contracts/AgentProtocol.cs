namespace RoomOS.Domain.Contracts;

/// <summary>
/// Protocole du hub agent. Défini une seule fois ici, référencé par le Core
/// et par l'agent Windows (voir docs/03-architecture.md).
/// </summary>
public static class AgentProtocol
{
    /// <summary>Méthodes appelées par l'agent sur le Core.</summary>
    public static class ToCore
    {
        public const string Register = "Register";
        public const string PushTelemetry = "PushTelemetry";
        public const string PushAudioState = "PushAudioState";
        public const string Ack = "Ack";
    }

    /// <summary>Méthodes appelées par le Core sur l'agent.</summary>
    public static class ToAgent
    {
        public const string Shutdown = "Shutdown";
        public const string Restart = "Restart";
        public const string SetAudioOutput = "SetAudioOutput";
        public const string SetVolume = "SetVolume";
        public const string SetMute = "SetMute";
    }
}

/// <summary>Premier message de l'agent après connexion.</summary>
public sealed record RegisterRequest(string PcId, string AgentVersion, long UptimeSec);

/// <summary>Acquittement d'une commande. L'agent acquitte **avant** d'exécuter.</summary>
public sealed record CommandAck(string CommandId, string Status, string? Message);

/// <summary>Commande envoyée à l'agent.</summary>
public sealed record AgentCommand(string CommandId);

/// <summary>État audio publié par l'agent, à chaque changement et en filet toutes les 10 s.</summary>
public sealed record AgentAudioState(
    string? ActiveWindowsDeviceId,
    int Volume,
    bool Muted,
    IReadOnlyList<WindowsAudioOutput> Outputs);

public sealed record SetAudioOutputCommand(string CommandId, string WindowsDeviceId);

public sealed record SetVolumeCommand(string CommandId, int Level);

public sealed record SetMuteCommand(string CommandId, bool Muted);
