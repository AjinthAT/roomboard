using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using RoomOS.Core.Configuration;

namespace RoomOS.Core.Auth;

/// <summary>
/// Authentification par jeton statique. Volontairement minimale, mais présente dès
/// M1 : un endpoint qui éteint un PC ne reste pas ouvert « parce qu'on est en LAN »
/// (ADR D9).
/// </summary>
public sealed class TokenAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory loggerFactory,
    UrlEncoder encoder,
    IOptions<RoomOsOptions> roomOptions)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, loggerFactory, encoder)
{
    public const string SchemeName = "Token";
    public const string ClientPolicy = "Client";
    public const string AgentPolicy = "Agent";
    public const string ClientRole = "client";
    public const string AgentRole = "agent";

    private readonly RoomOsOptions _roomOptions = roomOptions.Value;

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var token = ReadToken(Request);

        if (string.IsNullOrEmpty(token))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var role = MatchRole(token);

        if (role is null)
        {
            return Task.FromResult(AuthenticateResult.Fail("Jeton invalide."));
        }

        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Role, role)], SchemeName);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    private string? MatchRole(string token)
    {
        // Comparaison à temps constant : ces jetons sont des secrets.
        if (IsConfigured(_roomOptions.ApiToken) && FixedTimeEquals(token, _roomOptions.ApiToken))
        {
            return ClientRole;
        }

        if (IsConfigured(_roomOptions.AgentToken) && FixedTimeEquals(token, _roomOptions.AgentToken))
        {
            return AgentRole;
        }

        return null;
    }

    private static bool IsConfigured(string token) => !string.IsNullOrWhiteSpace(token);

    private static bool FixedTimeEquals(string left, string right) =>
        System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.UTF8.GetBytes(left),
            System.Text.Encoding.UTF8.GetBytes(right));

    /// <summary>
    /// Le client SignalR JavaScript ne peut pas poser d'en-tête sur le WebSocket :
    /// il passe le jeton en query string. Les deux formes sont donc acceptées
    /// (docs/05-api.md).
    /// </summary>
    private static string? ReadToken(HttpRequest request)
    {
        var header = request.Headers.Authorization.ToString();

        if (header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return header["Bearer ".Length..].Trim();
        }

        if (request.Path.StartsWithSegments("/hub") &&
            request.Query.TryGetValue("access_token", out var fromQuery))
        {
            return fromQuery.ToString();
        }

        return null;
    }
}
