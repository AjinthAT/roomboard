using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using RoomOS.Domain.Contracts;
using Xunit;

namespace RoomOS.Core.Tests;

/// <summary>
/// Vérifie que l'application démarre, que /healthz reste ouvert et que l'API
/// est bien fermée sans jeton.
/// </summary>
public sealed class HealthEndpointTests(RoomOsWebFactory factory) : IClassFixture<RoomOsWebFactory>
{
    [Fact]
    public async Task Healthz_repond_200_sans_authentification()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/healthz", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<HealthResponse>(
            TestContext.Current.CancellationToken);

        Assert.NotNull(body);
        Assert.Equal("ok", body.Status);
    }

    [Fact]
    public async Task L_api_est_fermee_sans_jeton()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/state", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Le_jeton_agent_ne_donne_pas_acces_aux_routes_client()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", RoomOsWebFactory.AgentToken);

        var response = await client.GetAsync("/api/state", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Le_snapshot_contient_la_piece_et_le_pc_seedes()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", RoomOsWebFactory.ApiToken);

        var snapshot = await client.GetFromJsonAsync<StateSnapshot>(
            "/api/state", TestContext.Current.CancellationToken);

        Assert.NotNull(snapshot);
        Assert.Equal("bedroom", snapshot.Room.Id);

        var pc = Assert.Single(snapshot.Pcs);
        Assert.False(pc.Online);
    }
}
