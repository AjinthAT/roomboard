using System.Net;
using System.Net.Sockets;

namespace RoomOS.Core.Integrations.Wol;

/// <summary>
/// Émet le paquet magique en broadcast UDP. C'est le Core qui l'envoie, pas l'agent :
/// quand le PC est éteint, il n'y a pas d'agent (docs/03-architecture.md).
/// </summary>
public sealed class WakeOnLanSender(ILogger<WakeOnLanSender> logger)
{
    // Port 9 (discard). Le port 7 fonctionne aussi ; on envoie sur les deux, certaines
    // cartes n'écoutent que l'un des deux.
    private static readonly int[] Ports = [9, 7];

    public async Task SendAsync(string mac, string broadcast, CancellationToken ct)
    {
        var packet = MagicPacket.Build(mac);
        var address = IPAddress.Parse(broadcast);

        using var client = new UdpClient { EnableBroadcast = true };

        foreach (var port in Ports)
        {
            await client.SendAsync(packet, new IPEndPoint(address, port), ct);
        }

        logger.LogInformation("Paquet magique envoyé à {Mac} via {Broadcast}.", mac, broadcast);
    }
}
