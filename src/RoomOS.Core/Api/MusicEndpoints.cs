using Microsoft.Extensions.Options;
using RoomOS.Core.Auth;
using RoomOS.Core.Configuration;
using RoomOS.Core.Data;
using RoomOS.Core.Integrations.Spotify;
using RoomOS.Core.State;
using RoomOS.Domain.Contracts;

namespace RoomOS.Core.Api;

public static class MusicEndpoints
{
    public sealed record PlayRequest(string? Uri);
    public sealed record VolumeRequest(int Level);
    public sealed record TransferRequest(string DeviceId);
    public sealed record ShuffleRequest(bool Enabled);
    public sealed record RepeatRequest(string Mode);
    public sealed record SeekRequest(int PositionMs);

    public static IEndpointRouteBuilder MapMusicEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/music")
            .RequireAuthorization(TokenAuthenticationHandler.ClientPolicy);

        group.MapGet("/now-playing", (StateStore state) => Results.Ok(state.Music));

        // Sert à renseigner ROOMOS__Spotify__PcDeviceHint sans deviner le nom que
        // Spotify donne à la machine (docs/09-integrations.md).
        group.MapGet("/devices", async (IMusicProvider music, CancellationToken ct) =>
            Results.Ok(await music.GetDevicesAsync(ct)));

        group.MapPost("/play", (PlayRequest? body, IMusicProvider music, CancellationToken ct) =>
            Guard(ct => music.PlayAsync(body?.Uri, ct), ct));

        group.MapPost("/pause", (IMusicProvider music, CancellationToken ct) =>
            Guard(music.PauseAsync, ct));

        group.MapPost("/next", (IMusicProvider music, CancellationToken ct) =>
            Guard(music.NextAsync, ct));

        group.MapPost("/previous", (IMusicProvider music, CancellationToken ct) =>
            Guard(music.PreviousAsync, ct));

        // Playlists mises en avant. Servies depuis la configuration : il n'y a pas
        // d'éditeur en V1, et coder des URI en dur dans le front serait pire.
        group.MapGet("/playlists", (IOptions<RoomOsOptions> options) =>
            Results.Ok(options.Value.Spotify.Playlists
                .Where(p => !string.IsNullOrWhiteSpace(p.Uri))
                .Select(p => new { p.Name, p.Uri })));

        group.MapPost("/transfer", (TransferRequest body, IMusicProvider music, CancellationToken ct) =>
            Guard(ct => music.TransferToDeviceAsync(body.DeviceId, ct), ct));

        group.MapPut("/shuffle", (ShuffleRequest body, IMusicProvider music, CancellationToken ct) =>
            Guard(ct => music.SetShuffleAsync(body.Enabled, ct), ct));

        group.MapPut("/repeat", (RepeatRequest body, IMusicProvider music, CancellationToken ct) =>
            body.Mode is "off" or "track" or "context"
                ? Guard(ct => music.SetRepeatAsync(body.Mode, ct), ct)
                : Task.FromResult(Results.BadRequest(new { message = "Mode inconnu." })));

        group.MapPut("/seek", (SeekRequest body, IMusicProvider music, CancellationToken ct) =>
            Guard(ct => music.SeekAsync(body.PositionMs, ct), ct));

        group.MapGet("/queue", async (IMusicProvider music, CancellationToken ct) =>
            Results.Ok(await music.GetQueueAsync(ct)));

        group.MapPost("/queue", (PlayRequest body, IMusicProvider music, CancellationToken ct) =>
            string.IsNullOrWhiteSpace(body.Uri)
                ? Task.FromResult(Results.BadRequest(new { message = "Il faut un uri." }))
                : Guard(ct => music.QueueAsync(body.Uri, ct), ct));

        // Plafonnée à dix résultats par Spotify, pas par nous.
        group.MapGet("/search", async (string q, IMusicProvider music, CancellationToken ct) =>
            Results.Ok(await music.SearchAsync(q, ct)));

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
