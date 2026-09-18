using LibreHardwareMonitor.Hardware;
using RoomOS.Domain.Contracts;

namespace RoomOS.Agent.Windows;

/// <summary>
/// Lit les capteurs matériels via LibreHardwareMonitor.
/// </summary>
/// <remarks>
/// <para>
/// On appelle <c>Update()</c> avant chaque lecture, sinon les valeurs sont figées.
/// </para>
/// <para>
/// La sélection se fait par <b>liste de noms ordonnée</b>, pas par « premier capteur
/// du bon type ». Un i7-12700K expose une quarantaine de capteurs de température,
/// dont des « Distance to TjMax » qui sont des écarts et non des températures ; un
/// GPU NVIDIA expose « GPU Core » et « GPU Memory Junction », dix degrés d'écart.
/// Prendre le premier venu donne une valeur plausible et fausse — le pire des cas.
/// </para>
/// <para>
/// Les noms changent d'une version de pilote à l'autre : chaque lecture a donc
/// plusieurs candidats, et toute température reste nullable jusqu'à l'UI.
/// </para>
/// </remarks>
public sealed class TelemetryReader(ILogger<TelemetryReader> logger) : IDisposable
{
    private readonly Computer _computer = new()
    {
        IsCpuEnabled = true,
        IsGpuEnabled = true,
        IsMemoryEnabled = true,
    };

    private bool _opened;
    private bool _warnedAboutTemperatures;

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
            logger.LogError(
                ex,
                "Impossible d'ouvrir les capteurs. Driver probablement bloqué par " +
                "l'intégrité de la mémoire (Sécurité Windows → Isolation du noyau). " +
                "L'agent continue sans températures.");
            return false;
        }
    }

    public Telemetry Read()
    {
        if (!_opened)
        {
            return new Telemetry(0, null, 0, null, null, null, 0, 0);
        }

        double cpuUsage = 0, gpuUsage = 0, ramUsedMb = 0, ramTotalMb = 0;
        double? cpuTemp = null, gpuTemp = null, vramUsed = null, vramTotal = null;

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
                    cpuUsage = Pick(hardware, SensorType.Load, "CPU Total") ?? cpuUsage;
                    cpuTemp = Pick(hardware, SensorType.Temperature,
                        "CPU Package", "Core Average", "Core Max") ?? cpuTemp;
                    break;

                case HardwareType.GpuNvidia:
                case HardwareType.GpuAmd:
                case HardwareType.GpuIntel:
                    gpuUsage = Pick(hardware, SensorType.Load, "GPU Core") ?? gpuUsage;
                    gpuTemp = Pick(hardware, SensorType.Temperature,
                        "GPU Core", "GPU Hot Spot", "GPU Temperature") ?? gpuTemp;
                    vramUsed = Pick(hardware, SensorType.SmallData, "GPU Memory Used") ?? vramUsed;
                    vramTotal = Pick(hardware, SensorType.SmallData, "GPU Memory Total") ?? vramTotal;
                    break;

                case HardwareType.Memory:
                    // Deux blocs [Memory] coexistent : « Total Memory » et « Virtual
                    // Memory ». Sans ce filtre, le second écrase le premier et on
                    // affiche le fichier d'échange à la place de la RAM.
                    if (!hardware.Name.Contains("Virtual", StringComparison.OrdinalIgnoreCase))
                    {
                        // Ces capteurs sont en Go.
                        var used = Pick(hardware, SensorType.Data, "Memory Used");
                        var available = Pick(hardware, SensorType.Data, "Memory Available");

                        ramUsedMb = used * 1024 ?? ramUsedMb;
                        ramTotalMb = (used + available) * 1024 ?? ramTotalMb;
                    }

                    break;
            }
        }

        WarnOnceIfNoTemperature(cpuTemp);

        return new Telemetry(
            cpuUsage, cpuTemp, gpuUsage, gpuTemp, vramUsed, vramTotal, ramUsedMb, ramTotalMb);
    }

    /// <summary>
    /// Premier capteur dont le nom correspond exactement à l'un des candidats, dans
    /// l'ordre donné. Renvoie <c>null</c> si aucun ne correspond ou si le capteur
    /// trouvé n'a pas de valeur : mieux vaut « — » à l'écran qu'un chiffre faux.
    /// </summary>
    private static double? Pick(IHardware hardware, SensorType type, params string[] names)
    {
        foreach (var name in names)
        {
            var sensor = hardware.Sensors.FirstOrDefault(
                s => s.SensorType == type && s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

            if (sensor?.Value is { } value)
            {
                return value;
            }
        }

        return null;
    }

    /// <summary>
    /// Les capteurs de température existent mais valent <c>null</c> quand le processus
    /// n'est pas élevé. C'est la cause la plus fréquente, et la moins évidente.
    /// </summary>
    private void WarnOnceIfNoTemperature(double? cpuTemp)
    {
        if (cpuTemp is not null || _warnedAboutTemperatures)
        {
            return;
        }

        _warnedAboutTemperatures = true;
        logger.LogWarning(
            "Aucune température CPU lisible. Deux causes, dans cet ordre : " +
            "PawnIO n'est pas installé (https://pawnio.eu — LibreHardwareMonitor ne " +
            "fournit plus de driver depuis la 0.9.5, il lit les MSR à travers celui-ci) ; " +
            "ou le processus n'est pas élevé. Lancer « --sensors » pour trancher.");
    }

    public void Dispose()
    {
        if (_opened)
        {
            _computer.Close();
        }
    }
}
