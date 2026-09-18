using System.Globalization;

namespace RoomOS.Core.Integrations.Wol;

/// <summary>
/// Construction du paquet magique Wake-on-LAN : six octets 0xFF, puis l'adresse
/// MAC répétée seize fois. Toujours 102 octets.
/// </summary>
public static class MagicPacket
{
    private const int MacLength = 6;
    private const int Repetitions = 16;
    public const int PacketLength = MacLength + (MacLength * Repetitions);

    public static byte[] Build(string mac)
    {
        var address = ParseMac(mac);
        var packet = new byte[PacketLength];

        packet.AsSpan(0, MacLength).Fill(0xFF);

        for (var i = 0; i < Repetitions; i++)
        {
            address.CopyTo(packet.AsSpan(MacLength + (i * MacLength)));
        }

        return packet;
    }

    /// <summary>
    /// Accepte <c>AA:BB:CC:DD:EE:FF</c>, <c>AA-BB-CC-DD-EE-FF</c> et <c>AABBCCDDEEFF</c>,
    /// en majuscules ou en minuscules. <c>getmac</c> sous Windows utilise le tiret.
    /// </summary>
    public static byte[] ParseMac(string mac)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mac);

        var cleaned = mac.Replace(":", string.Empty).Replace("-", string.Empty).Trim();

        if (cleaned.Length != MacLength * 2)
        {
            throw new FormatException($"Adresse MAC invalide : « {mac} ».");
        }

        var address = new byte[MacLength];

        for (var i = 0; i < MacLength; i++)
        {
            var slice = cleaned.AsSpan(i * 2, 2);

            if (!byte.TryParse(slice, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
            {
                throw new FormatException($"Adresse MAC invalide : « {mac} ».");
            }

            address[i] = value;
        }

        return address;
    }
}
