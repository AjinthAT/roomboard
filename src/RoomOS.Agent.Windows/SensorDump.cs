using LibreHardwareMonitor.Hardware;

namespace RoomOS.Agent.Windows;

/// <summary>
/// Mode diagnostic : liste tout ce que LibreHardwareMonitor voit sur la machine.
/// Sert à comprendre pourquoi une valeur manque, sans deviner les noms de capteurs.
/// </summary>
public static class SensorDump
{
    public static int Run()
    {
        var computer = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true,
            IsMemoryEnabled = true,
            IsMotherboardEnabled = true,
        };

        try
        {
            computer.Open();
        }
        catch (Exception ex)
        {
            Console.WriteLine("ECHEC de l'ouverture des capteurs :");
            Console.WriteLine($"  {ex.GetType().Name} : {ex.Message}");
            Console.WriteLine();
            Console.WriteLine("Causes habituelles :");
            Console.WriteLine("  - l'exe n'est pas lancé en administrateur ;");
            Console.WriteLine("  - l'intégrité de la mémoire bloque le driver noyau");
            Console.WriteLine("    (Sécurité Windows > Sécurité de l'appareil > Isolation du noyau).");
            return 1;
        }

        Console.WriteLine($"Administrateur : {IsAdministrator()}");
        Console.WriteLine($"PawnIO         : {(IsPawnIoInstalled() ? "installé" : "ABSENT — https://pawnio.eu")}");
        Console.WriteLine();

        if (!IsPawnIoInstalled())
        {
            Console.WriteLine("Sans PawnIO, aucune lecture de registre MSR n'est possible :");
            Console.WriteLine("  températures, fréquences et puissances CPU sortiront toutes à « null ».");
            Console.WriteLine("  Charges CPU, RAM et capteurs GPU fonctionnent sans lui.");
            Console.WriteLine();
        }

        foreach (var hardware in computer.Hardware)
        {
            Dump(hardware, indent: 0);
        }

        computer.Close();
        Console.WriteLine();
        Console.WriteLine("Colle cette sortie entière dans la conversation.");
        return 0;
    }

    private static void Dump(IHardware hardware, int indent)
    {
        hardware.Update();

        var pad = new string(' ', indent);
        Console.WriteLine($"{pad}[{hardware.HardwareType}] {hardware.Name}");

        foreach (var sensor in hardware.Sensors.OrderBy(s => s.SensorType).ThenBy(s => s.Name))
        {
            var value = sensor.Value is { } v ? v.ToString("0.##") : "null";
            Console.WriteLine($"{pad}   {sensor.SensorType,-12} | {sensor.Name,-32} = {value}");
        }

        foreach (var sub in hardware.SubHardware)
        {
            Dump(sub, indent + 2);
        }
    }

    /// <summary>
    /// PawnIO s'installe en service noyau. LibreHardwareMonitor ne fournit plus de
    /// driver depuis la 0.9.5 : il embarque des modules bytecode et les exécute dans
    /// PawnIO, qui est signé et compatible avec l'intégrité de la mémoire.
    /// </summary>
    private static bool IsPawnIoInstalled()
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        return File.Exists(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.System), "drivers", "PawnIO.sys"));
    }

    private static bool IsAdministrator()
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
        return new System.Security.Principal.WindowsPrincipal(identity)
            .IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
    }
}
