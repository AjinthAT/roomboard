using RoomOS.Core.Integrations.Mqtt;
using Xunit;

namespace RoomOS.Core.Tests;

/// <summary>
/// Les conversions d'unités entre Zigbee2MQTT et RoomOS sont du calcul pur, et une
/// erreur y serait invisible : une couleur légèrement fausse ne lève aucune exception.
/// </summary>
public sealed class ZigbeeColorTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(254, 100)]
    [InlineData(127, 50)]
    public void La_luminosite_zigbee_se_convertit_en_pourcentage(int zigbee, int percent)
    {
        Assert.Equal(percent, ZigbeeColor.ToPercent(zigbee));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(100, 254)]
    [InlineData(50, 127)]
    public void Le_pourcentage_se_convertit_en_luminosite_zigbee(int percent, int zigbee)
    {
        Assert.Equal(zigbee, ZigbeeColor.ToZigbeeBrightness(percent));
    }

    [Fact]
    public void Les_valeurs_hors_bornes_sont_ramenees_dans_l_intervalle()
    {
        Assert.Equal(100, ZigbeeColor.ToPercent(9999));
        Assert.Equal(0, ZigbeeColor.ToPercent(-5));
        Assert.Equal(254, ZigbeeColor.ToZigbeeBrightness(300));
        Assert.Equal(0, ZigbeeColor.ToZigbeeBrightness(-1));
    }

    /// <summary>Points de référence du gamut sRGB, coordonnées CIE 1931.</summary>
    [Theory]
    [InlineData(0.64, 0.33, "FF")]   // rouge primaire : composante rouge saturée
    [InlineData(0.3, 0.6, "00")]     // vert primaire : composante rouge nulle
    public void Une_couleur_xy_donne_un_hexadecimal_plausible(double x, double y, string expectedRed)
    {
        var hex = ZigbeeColor.FromXy(x, y);

        Assert.Equal(7, hex.Length);
        Assert.Equal('#', hex[0]);
        Assert.Equal(expectedRed, hex[1..3]);
    }

    [Fact]
    public void Une_coordonnee_degeneree_ne_fait_pas_exploser_le_calcul()
    {
        Assert.Equal("#000000", ZigbeeColor.FromXy(0.3, 0));
    }

    [Theory]
    [InlineData("#FF6A00", true)]
    [InlineData("#ff6a00", true)]
    [InlineData("FF6A00", false)]
    [InlineData("#FF6A0", false)]
    [InlineData("#GGGGGG", false)]
    [InlineData(null, false)]
    public void Un_hexadecimal_invalide_est_rejete(string? hex, bool valid)
    {
        Assert.Equal(valid, ZigbeeColor.IsValidHex(hex));
    }
}
