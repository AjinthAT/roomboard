using LibreHardwareMonitor.Hardware;
using RoomOS.Domain.Contracts;

namespace RoomOS.Agent.Windows;

/// <summary>
/// Lit les capteurs matériels via LibreHardwareMonitor.
/// </summary>
/// <remarks>
/// Deux règles tirées de docs/06-agent-windows.md :
/// on appelle <c>Update()</c> avant chaque lecture, sinon les valeurs sont figées ;
/// et on ne matche jamais un capteur sur son nom, parce que les noms changent d'une
/// version de pilote GPU à l'autre. Toute température est nullable de bout en bout :
/// si un capteur disparaît, l'agent publie quand même l'usage et la RAM.
/// </remarks>
public sealed class TelemetryReader : IDisposable
{
    private readonly Computer _computer;
    private readonly ILogger<TelemetryReader> _logger;
    private bool _opened;

    public TelemetryReader(ILogger<TelemetryReader> logger)
    {
        _logger = logger;
        _computer = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true,
            IsMemoryEnabled = true,
        };
    }

    /// <summary>
    /// Ouvre l'accès aux capteurs. Échoue si le driver noyau ne peut pas être chargé,
    /// typiquement quand l'intégrité de la mémoire est active sur Windows 11.
    /// </summary>
    public bool TryOpen()
    {
        try
        {
            _computer.Open();
            _opened = true;
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Impossible d'ouvrir les capteurs. Le driver est probablement bloqué par " +
                "l'intégrité de la mémoire (Sécurité Windows → Isolation du noyau). " +
                "L'agent continue sans températures.");
            return false;
        }
    }

    public Telemetry Read()
    {
        double cpuUsage = 0, gpuUsage = 0, ramUsed = 0, ramTotal = 0;
        double? cpuTemp = null, gpuTemp = null, vramUsed = null, vramTotal = null;

        if (!_opened)
        {
            return new Telemetry(0, null, 0, null, null, null, 0, 0);
        }

        foreach (var hardware in _computer.Hardware)
        {
            hardware.Update();

            foreach (var sub in hardware.SubHardware)
            {
                sub.Update();
            }

            switch (hardware.HardwareType)
            {
                case HardwareType.Cpu:
                    cpuUsage = FindValue(hardware, SensorType.Load, "total") ?? cpuUsage;
                    cpuTemp = FindFirst(hardware, SensorType.Temperature) ?? cpuTemp;
                    break;

                case HardwareType.GpuNvidia:
                case HardwareType.GpuAmd:
                case HardwareType.GpuIntel:
                    gpuUsage = FindValue(hardware, SensorType.Load, "core") ?? gpuUsage;
                    gpuTemp = FindFirst(hardware, SensorType.Temperature) ?? gpuTemp;
                    vramUsed = FindValue(hardware, SensorType.SmallData, "used") ?? vramUsed;
                    vramTotal = FindValue(hardware, SensorType.SmallData, "total") ?? vramTotal;
                    break;

                case HardwareType.Memory:
                    // LHM expose « Memory Used » et « Memory Available » en Go.
                    // Recherche stricte : sans repli, un capteur manquant donnerait
                    // un total égal à deux fois l'utilisé.
                    var used = FindExact(hardware, SensorType.Data, "used");
                    var available = FindExact(hardware, SensorType.Data, "available");

                    ramUsed = used * 1024 ?? ramUsed;
                    ramTotal = (used + available) * 1024 ?? ramTotal;
                    break;
            }
        }

        return new Telemetry(cpuUsage, cpuTemp, gpuUsage, gpuTemp, vramUsed, vramTotal, ramUsed, ramTotal);
    }

    /// <summary>
    /// Capteur du type demandé dont le nom contient l'indice, sans repli.
    /// Renvoie <c>null</c> plutôt qu'une valeur approchée.
    /// </summary>
    private static double? FindExact(IHardware hardware, SensorType type, string hint) =>
        hardware.Sensors
            .FirstOrDefault(s => s.SensorType == type &&
                                 s.Name.Contains(hint, StringComparison.OrdinalIgnoreCase))
            ?.Value;

    /// <summary>Premier capteur du type demandé, quel que soit son nom.</summary>
    private static double? FindFirst(IHardware hardware, SensorType type) =>
        hardware.Sensors.FirstOrDefault(s => s.SensorType == type)?.Value;

    /// <summary>
    /// Capteur du type demandé dont le nom contient un indice. L'indice est un filet,
    /// pas une clé : si rien ne correspond, on retombe sur le premier capteur du type.
    /// </summary>
    private static double? FindValue(IHardware hardware, SensorType type, string hint)
    {
        var sensors = hardware.Sensors.Where(s => s.SensorType == type).ToList();

        var match = sensors.FirstOrDefault(
            s => s.Name.Contains(hint, StringComparison.OrdinalIgnoreCase));

        return match?.Value ?? sensors.FirstOrDefault()?.Value;
    }

    public void Dispose()
    {
        if (_opened)
        {
            _computer.Close();
        }
    }
}
