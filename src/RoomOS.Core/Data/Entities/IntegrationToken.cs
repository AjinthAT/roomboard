namespace RoomOS.Core.Data.Entities;

/// <summary>
/// Jetons OAuth d'une intégration. C'est la seule donnée sensible persistée, et
/// elle n'a pas le choix : un <c>refresh_token</c> doit survivre au redémarrage,
/// sinon il faudrait réautoriser à chaque fois.
/// </summary>
public sealed class IntegrationToken
{
    public const string Spotify = "spotify";

    public required string Provider { get; set; }
    public required string AccessToken { get; set; }
    public required string RefreshToken { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
}
