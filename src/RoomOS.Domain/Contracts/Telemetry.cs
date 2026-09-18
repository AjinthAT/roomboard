namespace RoomOS.Domain.Contracts;

/// <summary>
/// Échantillon de télémétrie publié par l'agent. Jamais persisté (voir ADR D6).
/// </summary>
/// <remarks>
/// Toutes les températures sont nullables de bout en bout : les capteurs
/// disparaissent d'une version de pilote GPU à l'autre. Voir docs/06-agent-windows.md.
/// </remarks>
public sealed record Telemetry(
    double CpuUsage,
    double? CpuTempC,
    double GpuUsage,
    double? GpuTempC,
    double? VramUsedMb,
    double? VramTotalMb,
    double RamUsedMb,
    double RamTotalMb);
