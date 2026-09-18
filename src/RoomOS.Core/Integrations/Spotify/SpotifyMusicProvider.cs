using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using RoomOS.Core.Data;
using RoomOS.Domain.Contracts;

namespace RoomOS.Core.Integrations.Spotify;

/// <summary>
/// Unique implémentation d'<see cref="IMusicProvider"/> en V1 (ADR D7).
/// </summary>
/// <remarks>
/// Tous les endpoints utilisés sont ceux de la section Player, les seuls conservés
/// après le durcissement de février 2026 (docs/09-integrations.md).
/// </remarks>
public sealed class SpotifyMusicProvider(
    IHttpClientFactory httpClientFactory,
    SpotifyAuthService auth,
    RoomOsDbContext db) : IMusicProvider
{
    public const string HttpClientName = "spotify";

    private const string ApiBase = "https://api.spotify.com/v1";

    public async Task<MusicState> GetStateAsync(CancellationToken ct)
    {
        var client = await AuthorizedClientAsync(ct);

        if (client is null)
        {
            return new MusicState(MusicLinkState.NotLinked, NowPlaying.Nothing);
        }

        using (client)
        {
            using var response = await client.GetAsync($"{ApiBase}/me/player", ct);

            // 204 : autorisé, mais aucun appareil ne joue. Ce n'est pas une erreur.
            if (response.StatusCode == HttpStatusCode.NoContent)
            {
                return new MusicState(MusicLinkState.NoActiveDevice, NowPlaying.Nothing);
            }

            // 401 : jeton révoqué. 403 : compte hors de la liste autorisée de
            // l'application. Les confondre avec « aucun appareil actif » enverrait
            // chercher la panne du mauvais côté.
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                return new MusicState(MusicLinkState.Denied, NowPlaying.Nothing);
            }

            // Tout autre échec est une panne : on lève, et l'appelant conserve le
            // dernier état connu plutôt que d'afficher une contre-vérité.
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"GET /me/player a répondu {(int)response.StatusCode}.");
            }

            var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);

            return new MusicState(MusicLinkState.Ready, Parse(json));
        }
    }

    public Task PlayAsync(string? uri, CancellationToken ct)
    {
        object? body = uri is null ? null
            : uri.Contains(":track:", StringComparison.Ordinal)
                ? new { uris = new[] { uri } }
                : new { context_uri = uri };

        return SendAsync(HttpMethod.Put, "/me/player/play", body, ct);
    }

    public Task PauseAsync(CancellationToken ct) =>
        SendAsync(HttpMethod.Put, "/me/player/pause", null, ct);

    public Task NextAsync(CancellationToken ct) =>
        SendAsync(HttpMethod.Post, "/me/player/next", null, ct);

    public Task PreviousAsync(CancellationToken ct) =>
        SendAsync(HttpMethod.Post, "/me/player/previous", null, ct);

    public Task SetVolumeAsync(int level, CancellationToken ct) =>
        SendAsync(HttpMethod.Put, $"/me/player/volume?volume_percent={Math.Clamp(level, 0, 100)}", null, ct);

    public async Task<IReadOnlyList<MusicDevice>> GetDevicesAsync(CancellationToken ct)
    {
        var client = await AuthorizedClientAsync(ct);

        if (client is null)
        {
            return [];
        }

        using (client)
        {
            using var response = await client.GetAsync($"{ApiBase}/me/player/devices", ct);

            if (!response.IsSuccessStatusCode)
            {
                return [];
            }

            var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);

            if (json.GetPropertyOrNull("devices") is not { ValueKind: JsonValueKind.Array } devices)
            {
                return [];
            }

            return [.. devices.EnumerateArray().Select(d => new MusicDevice(
                d.GetPropertyOrNull("id")?.GetString() ?? string.Empty,
                d.GetPropertyOrNull("name")?.GetString() ?? string.Empty,
                d.GetPropertyOrNull("is_active")?.GetBoolean() ?? false,
                d.GetPropertyOrNull("type")?.GetString() ?? string.Empty))];
        }
    }

    public async Task TransferToAsync(string hint, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(hint))
        {
            throw new InvalidOperationException(
                "ROOMOS__Spotify__PcDeviceHint n'est pas configuré.");
        }

        var devices = await GetDevicesAsync(ct);

        var target = devices.FirstOrDefault(
            d => d.Name.Contains(hint, StringComparison.OrdinalIgnoreCase));

        if (target is null)
        {
            throw new NoActiveMusicDeviceException();
        }

        if (target.IsActive)
        {
            // Idempotence : relancer une scène alors que tout est déjà en place ne
            // doit rien casser (docs/08-scenes.md, règle 6).
            return;
        }

        // play: false — on transfère sans forcer la lecture. L'étape music.play qui
        // suit décide, ou n'existe pas.
        await SendAsync(HttpMethod.Put, "/me/player",
            new { device_ids = new[] { target.Id }, play = false }, ct);
    }

    private async Task SendAsync(HttpMethod method, string path, object? body, CancellationToken ct)
    {
        var client = await AuthorizedClientAsync(ct)
            ?? throw new MusicNotLinkedException();

        using (client)
        {
            using var request = new HttpRequestMessage(method, ApiBase + path);

            if (body is not null)
            {
                request.Content = JsonContent.Create(body);
            }

            using var response = await client.SendAsync(request, ct);

            // 404 sur les commandes Player signifie « aucun appareil actif », pas
            // « route inconnue ». À traiter explicitement plutôt qu'en erreur muette.
            if (response.StatusCode is HttpStatusCode.NotFound)
            {
                throw new NoActiveMusicDeviceException();
            }

            if (!response.IsSuccessStatusCode)
            {
                var detail = await response.Content.ReadAsStringAsync(ct);
                throw new InvalidOperationException(
                    $"Spotify a répondu {(int)response.StatusCode} sur {path} : {detail}");
            }
        }
    }

    private async Task<HttpClient?> AuthorizedClientAsync(CancellationToken ct)
    {
        if (!auth.IsConfigured)
        {
            return null;
        }

        var token = await auth.GetAccessTokenAsync(db, ct);

        if (token is null)
        {
            return null;
        }

        var client = httpClientFactory.CreateClient(HttpClientName);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return client;
    }

    private static NowPlaying Parse(JsonElement player)
    {
        if (!player.TryGetProperty("item", out var item) || item.ValueKind != JsonValueKind.Object)
        {
            return NowPlaying.Nothing;
        }

        return new NowPlaying(
            Title: item.GetPropertyOrNull("name")?.GetString(),
            Artist: JoinArtists(item),
            AlbumArtUrl: SmallestCover(item),
            IsPlaying: player.GetPropertyOrNull("is_playing")?.GetBoolean() ?? false,
            ProgressMs: player.GetPropertyOrNull("progress_ms")?.GetInt32(),
            DurationMs: item.GetPropertyOrNull("duration_ms")?.GetInt32(),
            DeviceName: player.GetPropertyOrNull("device")?.GetPropertyOrNull("name")?.GetString());
    }

    private static string? JoinArtists(JsonElement item)
    {
        if (item.GetPropertyOrNull("artists") is not { ValueKind: JsonValueKind.Array } artists)
        {
            return null;
        }

        var names = artists.EnumerateArray()
            .Select(a => a.GetPropertyOrNull("name")?.GetString())
            .Where(n => !string.IsNullOrEmpty(n));

        var joined = string.Join(", ", names);

        return joined.Length == 0 ? null : joined;
    }

    /// <summary>
    /// La plus petite pochette disponible : l'iPad 5 a 2 Go de RAM, et une image de
    /// 640 pixels pour une vignette est du gaspillage (docs/07-frontend.md).
    /// </summary>
    private static string? SmallestCover(JsonElement item)
    {
        if (item.GetPropertyOrNull("album")?.GetPropertyOrNull("images")
            is not { ValueKind: JsonValueKind.Array } images)
        {
            return null;
        }

        return images.EnumerateArray()
            .OrderBy(i => i.GetPropertyOrNull("width")?.GetInt32() ?? int.MaxValue)
            .Select(i => i.GetPropertyOrNull("url")?.GetString())
            .FirstOrDefault(url => url is not null);
    }
}

file static class JsonElementExtensions
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
