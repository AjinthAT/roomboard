using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using RoomOS.Core.Api;
using RoomOS.Domain.Contracts;
using Xunit;

namespace RoomOS.Core.Tests;

/// <summary>
/// L'éditeur de scènes écrit dans la base ce que le moteur exécutera plus tard.
/// Une étape incomplète acceptée ici échouerait au pire moment : le soir, sans
/// personne devant un journal. D'où la validation à l'écriture, vérifiée ici.
/// </summary>
public sealed class SceneCrudTests(RoomOsWebFactory factory) : IClassFixture<RoomOsWebFactory>
{
    private HttpClient Client()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", RoomOsWebFactory.ApiToken);
        return client;
    }

    private static object Body(params object[] steps) =>
        new { name = "Test " + Guid.NewGuid().ToString("n")[..6], steps };

    [Fact]
    public async Task Une_scene_creee_se_relit_avec_ses_etapes()
    {
        using var client = Client();
        var ct = TestContext.Current.CancellationToken;

        var created = await client.PostAsJsonAsync("/api/scenes",
            new { name = "Lecture test", steps = new[] { new { type = "delay", ms = 500 } } }, ct);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var detail = await client.GetFromJsonAsync<SceneEndpoints.SceneDetail>(
            "/api/scenes/lecture-test", ct);

        Assert.NotNull(detail);
        var step = Assert.Single(detail.Steps);
        Assert.Equal(SceneStepTypes.Delay, step.Type);
        Assert.Equal(500, step.Ms);

        var deleted = await client.DeleteAsync("/api/scenes/lecture-test", ct);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
    }

    [Fact]
    public async Task Un_type_d_etape_inconnu_est_refuse()
    {
        using var client = Client();

        var response = await client.PostAsJsonAsync("/api/scenes",
            Body(new { type = "pc.explode" }), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Une_etape_sans_appareil_est_refusee()
    {
        using var client = Client();

        var response = await client.PostAsJsonAsync("/api/scenes",
            Body(new { type = "light.set", on = true }), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Une_sortie_audio_manquante_est_refusee()
    {
        using var client = Client();

        var response = await client.PostAsJsonAsync("/api/scenes",
            Body(new { type = "audio.setOutput", deviceId = "pc" }),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Un_appareil_inconnu_est_refuse()
    {
        using var client = Client();

        var response = await client.PostAsJsonAsync("/api/scenes",
            Body(new { type = "light.set", deviceId = "lampe-fantome", on = true }),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Une_scene_utilisee_par_une_routine_ne_se_supprime_pas()
    {
        using var client = Client();
        var ct = TestContext.Current.CancellationToken;

        await client.PostAsJsonAsync("/api/scenes",
            new { name = "Cible", steps = new[] { new { type = "music.pause" } } }, ct);

        var routine = await client.PostAsJsonAsync("/api/routines",
            new { name = "Test", sceneId = "cible", time = "07:00" }, ct);

        Assert.Equal(HttpStatusCode.Created, routine.StatusCode);

        var refused = await client.DeleteAsync("/api/scenes/cible", ct);
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);

        var id = (await routine.Content.ReadFromJsonAsync<Created>(ct))!.Id;
        await client.DeleteAsync($"/api/routines/{id}", ct);

        var accepted = await client.DeleteAsync("/api/scenes/cible", ct);
        Assert.Equal(HttpStatusCode.NoContent, accepted.StatusCode);
    }

    private sealed record Created(string Id);
}
