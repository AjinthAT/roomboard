using System.Net;
using Xunit;

namespace RoomOS.Core.Tests;

/// <summary>
/// L'export Prometheus est écrit à la main : une erreur de format ne lève rien, elle
/// se traduit par un scrutateur qui ignore silencieusement la cible.
/// </summary>
public sealed class MetricsEndpointTests(RoomOsWebFactory factory) : IClassFixture<RoomOsWebFactory>
{
    [Fact]
    public async Task Metrics_repond_200_sans_authentification()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/metrics", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Le_format_respecte_la_convention_prometheus()
    {
        using var client = factory.CreateClient();

        var body = await client.GetStringAsync("/metrics", TestContext.Current.CancellationToken);

        // Sans agent connecté il reste les jauges globales, toujours présentes.
        Assert.Contains("# HELP roomos_music_playing", body);
        Assert.Contains("# TYPE roomos_music_playing gauge", body);
        Assert.Contains("roomos_agents_connected 0", body);

        foreach (var line in body.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.StartsWith('#'))
            {
                Assert.Matches(@"^# (HELP|TYPE) \w+ ", line);
                continue;
            }

            // Un échantillon : nom, libellés facultatifs, espace, valeur décimale.
            Assert.Matches(@"^\w+(\{[^}]*\})? -?\d+(\.\d+)?$", line.TrimEnd('\r'));
        }
    }

    [Fact]
    public async Task Les_valeurs_utilisent_le_point_decimal()
    {
        using var client = factory.CreateClient();

        var body = await client.GetStringAsync("/metrics", TestContext.Current.CancellationToken);

        // Une virgule décimale, héritée de la culture de la machine, rendrait
        // l'export illisible pour Prometheus. La VM tourne en français : le risque
        // est réel, pas théorique.
        var samples = body
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Where(l => !l.StartsWith('#'))
            .ToList();

        Assert.NotEmpty(samples);
        Assert.All(samples, line => Assert.DoesNotMatch(@"\d,\d", line));
    }
}
