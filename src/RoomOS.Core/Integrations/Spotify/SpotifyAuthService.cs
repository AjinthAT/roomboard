using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RoomOS.Core.Configuration;
using RoomOS.Core.Data;
using RoomOS.Core.Data.Entities;

namespace RoomOS.Core.Integrations.Spotify;

/// <summary>
/// Flux Authorization Code + PKCE, et renouvellement du jeton d'accès.
/// </summary>
/// <remarks>
/// PKCE plutôt que le flux avec secret client : le Core est une machine domestique,
/// un secret y serait stocké sans bénéfice. Le <c>refresh_token</c> vit en base et
/// n'en sort jamais — le front ne voit aucun jeton Spotify (docs/03-architecture.md).
/// </remarks>
public sealed class SpotifyAuthService(
    IHttpClientFactory httpClientFactory,
    IOptions<RoomOsOptions> options,
    TimeProvider time,
    ILogger<SpotifyAuthService> logger)
{
    private const string AuthorizeUrl = "https://accounts.spotify.com/authorize";
    private const string TokenUrl = "https://accounts.spotify.com/api/token";

    private const string Scopes =
        "user-read-playback-state user-modify-playback-state user-read-currently-playing";

    /// <summary>
    /// Vérificateurs PKCE en attente, indexés par <c>state</c>. En mémoire : le flux
    /// se termine en quelques secondes, et un redémarrage du Core au milieu se résout
    /// en relançant l'autorisation.
    /// </summary>
    private readonly ConcurrentDictionary<string, (string Verifier, DateTimeOffset CreatedAt)>
        _pendingVerifiers = new();

    /// <summary>
    /// Durée de vie d'un vérificateur en attente. Une autorisation abandonnée — onglet
    /// fermé avant de valider — laisserait sinon une entrée à vie, et la route
    /// d'autorisation n'est pas authentifiée.
    /// </summary>
    private static readonly TimeSpan VerifierLifetime = TimeSpan.FromMinutes(10);

    private readonly SpotifyOptions _spotify = options.Value.Spotify;

    public bool IsConfigured => _spotify.IsConfigured;

    public string BuildAuthorizeUrl()
    {
        var verifier = RandomUrlSafe(64);
        var state = RandomUrlSafe(16);

        PurgeExpiredVerifiers();
        _pendingVerifiers[state] = (verifier, time.GetUtcNow());

        var query = new Dictionary<string, string?>
        {
            ["client_id"] = _spotify.ClientId,
            ["response_type"] = "code",
            ["redirect_uri"] = _spotify.RedirectUri,
            ["code_challenge_method"] = "S256",
            ["code_challenge"] = Challenge(verifier),
            ["scope"] = Scopes,
            ["state"] = state,
        };

        return Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString(AuthorizeUrl, query);
    }

    public async Task<bool> ExchangeCodeAsync(
        string code, string state, RoomOsDbContext db, CancellationToken ct)
    {
        if (!_pendingVerifiers.TryRemove(state, out var pending))
        {
            logger.LogWarning("Callback Spotify avec un state inconnu ou expiré.");
            return false;
        }

        if (time.GetUtcNow() - pending.CreatedAt > VerifierLifetime)
        {
            logger.LogWarning("Callback Spotify arrivé après expiration du vérificateur.");
            return false;
        }

        var verifier = pending.Verifier;

        var response = await PostTokenAsync(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = _spotify.RedirectUri,
            ["client_id"] = _spotify.ClientId,
            ["code_verifier"] = verifier,
        }, ct);

        if (response is null || response.RefreshToken is null)
        {
            return false;
        }

        var existing = await db.IntegrationTokens
            .FirstOrDefaultAsync(t => t.Provider == IntegrationToken.Spotify, ct);

        var expiresAt = time.GetUtcNow().AddSeconds(response.ExpiresIn);

        if (existing is null)
        {
            db.IntegrationTokens.Add(new IntegrationToken
            {
                Provider = IntegrationToken.Spotify,
                AccessToken = response.AccessToken,
                RefreshToken = response.RefreshToken,
                ExpiresAt = expiresAt,
            });
        }
        else
        {
            existing.AccessToken = response.AccessToken;
            existing.RefreshToken = response.RefreshToken;
            existing.ExpiresAt = expiresAt;
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Spotify autorisé, jetons enregistrés.");

        return true;
    }

    /// <summary>
    /// Jeton d'accès valide, renouvelé si nécessaire. <c>null</c> si l'intégration
    /// n'a jamais été autorisée.
    /// </summary>
    public async Task<string?> GetAccessTokenAsync(RoomOsDbContext db, CancellationToken ct)
    {
        var token = await db.IntegrationTokens
            .FirstOrDefaultAsync(t => t.Provider == IntegrationToken.Spotify, ct);

        if (token is null)
        {
            return null;
        }

        // Marge d'une minute : un jeton qui expire pendant l'appel coûte un aller-retour
        // et une erreur visible à l'écran.
        if (token.ExpiresAt > time.GetUtcNow().AddMinutes(1))
        {
            return token.AccessToken;
        }

        var refreshed = await PostTokenAsync(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = token.RefreshToken,
            ["client_id"] = _spotify.ClientId,
        }, ct);

        if (refreshed is null)
        {
            logger.LogError("Renouvellement du jeton Spotify refusé. Réautorisation nécessaire.");
            return null;
        }

        token.AccessToken = refreshed.AccessToken;
        token.ExpiresAt = time.GetUtcNow().AddSeconds(refreshed.ExpiresIn);

        // Spotify ne renvoie pas toujours un nouveau refresh_token : ne pas écraser
        // l'ancien avec une valeur vide sous peine de devoir tout réautoriser.
        if (!string.IsNullOrEmpty(refreshed.RefreshToken))
        {
            token.RefreshToken = refreshed.RefreshToken;
        }

        await db.SaveChangesAsync(ct);

        return token.AccessToken;
    }

    private async Task<TokenResponse?> PostTokenAsync(
        Dictionary<string, string> form, CancellationToken ct)
    {
        using var client = httpClientFactory.CreateClient(SpotifyMusicProvider.HttpClientName);
        using var content = new FormUrlEncodedContent(form);

        var response = await client.PostAsync(TokenUrl, content, ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            logger.LogError("Échange de jeton Spotify : {Status} {Body}", response.StatusCode, body);
            return null;
        }

        return await response.Content.ReadFromJsonAsync<TokenResponse>(ct);
    }

    private void PurgeExpiredVerifiers()
    {
        var deadline = time.GetUtcNow() - VerifierLifetime;

        foreach (var (key, pending) in _pendingVerifiers)
        {
            if (pending.CreatedAt < deadline)
            {
                _pendingVerifiers.TryRemove(key, out _);
            }
        }
    }

    private static string RandomUrlSafe(int bytes) =>
        Base64Url(RandomNumberGenerator.GetBytes(bytes));

    private static string Challenge(string verifier) =>
        Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

    private static string Base64Url(byte[] data) =>
        Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("refresh_token")] string? RefreshToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);
}
