namespace RoomOS.Domain.Contracts;

/// <summary>
/// Dernier état connu d'un PC. Vit en mémoire uniquement (voir docs/03-architecture.md).
/// </summary>
/// <remarks>
/// <paramref name="Online"/> reflète la présence de la connexion du hub agent, pas un
/// ping. C'est le signal le plus fiable disponible : si l'agent est connecté, le PC
/// est allumé et joignable.
/// </remarks>
public sealed record PcState(
    bool Online,
    TimeSpan? Uptime,
    Telemetry? Telemetry,
    DateTimeOffset UpdatedAt)
{
    public static PcState Offline(DateTimeOffset at) => new(false, null, null, at);
}
