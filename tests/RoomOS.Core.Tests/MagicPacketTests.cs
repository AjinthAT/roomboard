using RoomOS.Core.Integrations.Wol;
using Xunit;

namespace RoomOS.Core.Tests;

/// <summary>
/// Tests exigés par docs/11-conventions.md : construction du paquet Wake-on-LAN.
/// </summary>
public sealed class MagicPacketTests
{
    private const string Mac = "AA:BB:CC:DD:EE:FF";

    private static readonly byte[] Expected = [0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF];

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
    [InlineData("AA:BB:CC:DD:EE:FF")]
    [InlineData("AA-BB-CC-DD-EE-FF")]
    [InlineData("aa-bb-cc-dd-ee-ff")]
    [InlineData("AABBCCDDEEFF")]
    public void Les_separateurs_et_la_casse_sont_acceptes(string mac)
    {
        Assert.Equal(Expected, MagicPacket.ParseMac(mac));
    }

    [Theory]
    [InlineData("AA:BB:CC:DD:EE")]
    [InlineData("AA:BB:CC:DD:EE:FF:12")]
    [InlineData("ZZ:BB:CC:DD:EE:FF")]
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
