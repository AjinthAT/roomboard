using NAudio.CoreAudioApi;

namespace RoomOS.Agent.Windows.Audio;

/// <summary>
/// Mode diagnostic audio : liste les sorties avec leur identifiant Windows exact,
/// et permet d'éprouver la bascule sans passer par le Core.
/// </summary>
public static class AudioDump
{
    public static int Run(string[] args)
    {
        using var enumerator = new MMDeviceEnumerator();

        string? defaultId = null;

        try
        {
            using var active = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Console);
            defaultId = active.ID;
            Console.WriteLine($"Sortie par défaut : {active.FriendlyName}");
            Console.WriteLine($"  volume {(int)Math.Round(active.AudioEndpointVolume.MasterVolumeLevelScalar * 100)} %" +
                              $", {(active.AudioEndpointVolume.Mute ? "coupé" : "actif")}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Aucune sortie par défaut lisible : {ex.Message}");
        }

        Console.WriteLine();
        Console.WriteLine("Sorties actives :");

        var index = 0;

        foreach (var device in enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
        {
            using (device)
            {
                var marker = device.ID == defaultId ? "*" : " ";
                Console.WriteLine($" {marker} [{index++}] {device.FriendlyName}");
                Console.WriteLine($"        {device.ID}");
            }
        }

        var target = args.SkipWhile(a => a != "--audio").Skip(1).FirstOrDefault();

        if (target is null)
        {
            Console.WriteLine();
            Console.WriteLine("Pour éprouver la bascule : --audio <numéro entre crochets>");
            Console.WriteLine("Colle cette sortie entière dans la conversation.");
            return 0;
        }

        return TrySwitch(enumerator, target);
    }

    private static int TrySwitch(MMDeviceEnumerator enumerator, string target)
    {
        var devices = enumerator
            .EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
            .ToList();

        if (!int.TryParse(target, out var wanted) || wanted < 0 || wanted >= devices.Count)
        {
            Console.WriteLine($"\n« {target} » n'est pas un numéro de sortie valide.");
            return 1;
        }

        var device = devices[wanted];
        Console.WriteLine($"\nBascule vers « {device.FriendlyName} »…");

        if (PolicyConfig.TrySetDefaultOutput(device.ID, out var error))
        {
            Console.WriteLine("  OK — vérifie que le son sort bien du bon périphérique.");
            return 0;
        }

        Console.WriteLine($"  ECHEC : {error}");
        Console.WriteLine("  IPolicyConfig a changé ou est bloquée : c'est le point le plus");
        Console.WriteLine("  fragile du projet, signale-le.");
        return 1;
    }
}
