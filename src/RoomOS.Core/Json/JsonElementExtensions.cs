using System.Text.Json;

namespace RoomOS.Core.Json;

/// <summary>
/// Lecture tolérante de JSON venu de l'extérieur.
/// </summary>
/// <remarks>
/// Spotify comme Zigbee2MQTT omettent des champs selon le contexte, et en ajoutent
/// au fil des versions. Naviguer avec <c>TryGetProperty</c> à chaque niveau rendrait
/// le code illisible ; déréférencer sans vérifier le ferait planter sur une charge
/// utile parfaitement légitime.
/// </remarks>
internal static class JsonElementExtensions
{
    public static JsonElement? GetPropertyOrNull(this JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(name, out var value) &&
        value.ValueKind is not JsonValueKind.Null
            ? value
            : null;

    public static JsonElement? GetPropertyOrNull(this JsonElement? element, string name) =>
        element?.GetPropertyOrNull(name);
}
