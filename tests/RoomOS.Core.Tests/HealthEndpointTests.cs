using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using RoomOS.Domain.Contracts;
using Xunit;

namespace RoomOS.Core.Tests;

/// <summary>
/// Test de fumée M0 : vérifie que l'application démarre et que /healthz répond.
/// Les tests exigés par docs/11-conventions.md (moteur de scènes, DSL, paquet WoL)
/// arrivent avec les jalons qui les concernent.
/// </summary>
public sealed class HealthEndpointTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Healthz_repond_200_avec_le_statut_ok()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/healthz", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<HealthResponse>(
            TestContext.Current.CancellationToken);

        Assert.NotNull(body);
        Assert.Equal("ok", body.Status);
    }
}
