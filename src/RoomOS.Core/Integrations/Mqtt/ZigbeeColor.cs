using System.Globalization;

namespace RoomOS.Core.Integrations.Mqtt;

/// <summary>
/// Conversions entre les unités de Zigbee2MQTT et celles de l'API RoomOS.
/// </summary>
/// <remarks>
/// Deux écarts d'unités, tous deux sources d'erreurs silencieuses :
/// la luminosité vaut 0 à 254 côté Zigbee et 0 à 100 côté RoomOS ; et Z2M accepte
/// une couleur en hexadécimal mais la <em>renvoie</em> en coordonnées CIE xy.
/// Sans conversion retour, la couleur affichée serait fausse dès qu'une lampe est
/// pilotée hors de RoomOS.
/// </remarks>
public static class ZigbeeColor
{
    private const int ZigbeeMaxBrightness = 254;

    public static int ToPercent(int zigbeeBrightness) =>
        (int)Math.Round(Math.Clamp(zigbeeBrightness, 0, ZigbeeMaxBrightness) * 100.0 / ZigbeeMaxBrightness);

    public static int ToZigbeeBrightness(int percent) =>
        (int)Math.Round(Math.Clamp(percent, 0, 100) * ZigbeeMaxBrightness / 100.0);

    /// <summary>
    /// Coordonnées CIE xy vers hexadécimal sRGB.
    /// </summary>
    /// <remarks>
    /// Chemin standard xyY → XYZ → sRGB linéaire → correction gamma. La luminance est
    /// fixée à 1 : la teinte seule nous intéresse, la luminosité est portée par un
    /// champ distinct. Les composantes sont normalisées quand la couleur sort du
    /// gamut sRGB, ce qui arrive couramment avec les LED saturées.
    /// </remarks>
    public static string FromXy(double x, double y)
    {
        if (y <= 0)
        {
            return "#000000";
        }

        var yy = 1.0;
        var xx = yy / y * x;
        var zz = yy / y * (1 - x - y);

        var r = (xx * 3.2406) + (yy * -1.5372) + (zz * -0.4986);
        var g = (xx * -0.9689) + (yy * 1.8758) + (zz * 0.0415);
        var b = (xx * 0.0557) + (yy * -0.2040) + (zz * 1.0570);

        var max = Math.Max(r, Math.Max(g, b));

        if (max > 1)
        {
            r /= max;
            g /= max;
            b /= max;
        }

        return $"#{Channel(r):X2}{Channel(g):X2}{Channel(b):X2}";
    }

    private static int Channel(double linear)
    {
        var clamped = Math.Clamp(linear, 0, 1);

        var corrected = clamped <= 0.0031308
            ? 12.92 * clamped
            : (1.055 * Math.Pow(clamped, 1 / 2.4)) - 0.055;

        return (int)Math.Round(Math.Clamp(corrected, 0, 1) * 255);
    }

    /// <summary>Valide un <c>#RRGGBB</c>. Z2M refuse silencieusement une forme invalide.</summary>
    public static bool IsValidHex(string? hex) =>
        hex is { Length: 7 } && hex[0] == '#' &&
        int.TryParse(hex.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _);
}
