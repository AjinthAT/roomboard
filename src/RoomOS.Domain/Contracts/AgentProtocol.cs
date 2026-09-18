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
        public const string Ack = "Ack";
    }

    /// <summary>Méthodes appelées par le Core sur l'agent.</summary>
    public static class ToAgent
    {
        public const string Shutdown = "Shutdown";
        public const string Restart = "Restart";
    }
}

/// <summary>Premier message de l'agent après connexion.</summary>
public sealed record RegisterRequest(string PcId, string AgentVersion, long UptimeSec);

/// <summary>Acquittement d'une commande. L'agent acquitte **avant** d'exécuter.</summary>
public sealed record CommandAck(string CommandId, string Status, string? Message);

/// <summary>Commande envoyée à l'agent.</summary>
public sealed record AgentCommand(string CommandId);
