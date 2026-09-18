using RoomOS.Core.Integrations.Wol;
using Xunit;

namespace RoomOS.Core.Tests;

/// <summary>
/// Tests exigés par docs/11-conventions.md : construction du paquet Wake-on-LAN.
/// </summary>
public sealed class MagicPacketTests
{
    private const string Mac = "C8:7F:54:68:BB:40";

    private static readonly byte[] Expected = [0xC8, 0x7F, 0x54, 0x68, 0xBB, 0x40];

    [Fact]
    public void Le_paquet_fait_toujours_102_octets()
    {
        Assert.Equal(102, MagicPacket.Build(Mac).Length);
    }

    [Fact]
    public void Le_paquet_commence_par_six_octets_a_0xFF()
    {
        var packet = MagicPacket.Build(Mac);

        Assert.All(packet[..6], b => Assert.Equal(0xFF, b));
    }

    [Fact]
    public void La_mac_est_repetee_seize_fois_apres_l_entete()
    {
        var packet = MagicPacket.Build(Mac);

        for (var i = 0; i < 16; i++)
        {
            Assert.Equal(Expected, packet[(6 + (i * 6))..(6 + ((i + 1) * 6))]);
        }
    }

    [Theory]
    [InlineData("C8:7F:54:68:BB:40")]
    [InlineData("C8-7F-54-68-BB-40")]
    [InlineData("c8-7f-54-68-bb-40")]
    [InlineData("C87F5468BB40")]
    public void Les_separateurs_et_la_casse_sont_acceptes(string mac)
    {
        Assert.Equal(Expected, MagicPacket.ParseMac(mac));
    }

    [Theory]
    [InlineData("C8:7F:54:68:BB")]
    [InlineData("C8:7F:54:68:BB:40:12")]
    [InlineData("ZZ:7F:54:68:BB:40")]
    public void Une_mac_malformee_est_rejetee(string mac)
    {
        Assert.Throws<FormatException>(() => MagicPacket.ParseMac(mac));
    }

    [Fact]
    public void Une_mac_vide_est_rejetee()
    {
        Assert.Throws<ArgumentException>(() => MagicPacket.ParseMac("  "));
    }
}
