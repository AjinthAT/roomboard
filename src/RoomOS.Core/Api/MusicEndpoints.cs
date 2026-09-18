using RoomOS.Core.Auth;
using RoomOS.Core.Data;
using RoomOS.Core.Integrations.Spotify;
using RoomOS.Core.State;
using RoomOS.Domain.Contracts;

namespace RoomOS.Core.Api;

public static class MusicEndpoints
{
    public sealed record PlayRequest(string? Uri);
    public sealed record VolumeRequest(int Level);

    public static IEndpointRouteBuilder MapMusicEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/music")
            .RequireAuthorization(TokenAuthenticationHandler.ClientPolicy);

        group.MapGet("/now-playing", (StateStore state) => Results.Ok(state.Music));

        group.MapPost("/play", (PlayRequest? body, IMusicProvider music, CancellationToken ct) =>
            Guard(ct => music.PlayAsync(body?.Uri, ct), ct));

        group.MapPost("/pause", (IMusicProvider music, CancellationToken ct) =>
            Guard(music.PauseAsync, ct));

        group.MapPost("/next", (IMusicProvider music, CancellationToken ct) =>
            Guard(music.NextAsync, ct));

        group.MapPost("/previous", (IMusicProvider music, CancellationToken ct) =>
            Guard(music.PreviousAsync, ct));

        group.MapPut("/volume", (VolumeRequest body, IMusicProvider music, CancellationToken ct) =>
            body.Level is < 0 or > 100
                ? Task.FromResult(Results.BadRequest(new { message = "Le volume va de 0 à 100." }))
                : Guard(ct => music.SetVolumeAsync(body.Level, ct), ct));

        // Hors du groupe authentifié : ces deux routes sont parcourues par un
        // navigateur qui n'a pas le jeton, et le flux est protégé par PKCE.
        app.MapGet("/api/music/authorize", Authorize);
        app.MapGet("/api/music/callback", Callback);

        return app;
    }

    /// <summary>
    /// Traduit les deux états attendus de Spotify en statuts explicites plutôt qu'en
    /// 500 silencieux : l'UI doit pouvoir dire « pas autorisé » et « aucun appareil
    /// actif » (docs/09-integrations.md).
    /// </summary>
    private static async Task<IResult> Guard(Func<CancellationToken, Task> action, CancellationToken ct)
    {
        try
        {
            await action(ct);
            return Results.Accepted();
        }
        catch (MusicNotLinkedException)
        {
            return Results.Conflict(new { message = "Spotify n'est pas encore autorisé.", reason = "not-linked" });
        }
        catch (NoActiveMusicDeviceException)
        {
            return Results.Conflict(new { message = "Aucun appareil Spotify actif.", reason = "no-device" });
        }
    }

    private static IResult Authorize(SpotifyAuthService auth) =>
        auth.IsConfigured
            ? Results.Redirect(auth.BuildAuthorizeUrl())
            : Results.Problem("ROOMOS__Spotify__ClientId n'est pas configuré.");

    private static async Task<IResult> Callback(
        string? code,
        string? state,
        string? error,
        SpotifyAuthService auth,
        RoomOsDbContext db,
        CancellationToken ct)
    {
        if (error is not null)
        {
            return Page($"Autorisation refusée : {error}");
        }

        if (code is null || state is null)
        {
            return Page("Réponse incomplète de Spotify.");
        }

        return await auth.ExchangeCodeAsync(code, state, db, ct)
            ? Page("Spotify est autorisé. Tu peux fermer cet onglet et revenir sur RoomOS.")
            : Page("L'échange de jeton a échoué. Relance /api/music/authorize.");
    }

    /// <summary>Page de fin de flux, lue une fois dans un navigateur.</summary>
    private static IResult Page(string message) =>
        Results.Content(
            $"<!doctype html><meta charset=\"utf-8\"><title>RoomOS</title>" +
            $"<body style=\"font-family:system-ui;background:#0a0a0b;color:#e8e8ea;padding:3rem\">" +
            $"<p>{System.Net.WebUtility.HtmlEncode(message)}</p>",
            "text/html");
}
