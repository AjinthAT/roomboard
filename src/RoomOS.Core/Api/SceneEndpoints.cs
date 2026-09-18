using Microsoft.EntityFrameworkCore;
using RoomOS.Core.Auth;
using RoomOS.Core.Data;
using RoomOS.Core.Scenes;
using RoomOS.Domain.Contracts;

namespace RoomOS.Core.Api;

public static class SceneEndpoints
{
    public static IEndpointRouteBuilder MapSceneEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/scenes")
            .RequireAuthorization(TokenAuthenticationHandler.ClientPolicy);

        group.MapGet("/", ListScenes);
        group.MapPost("/{id}/run", RunScene);
        group.MapGet("/runs/{runId}", GetRun);

        return app;
    }

    public static async Task<List<SceneInfo>> ListAsync(RoomOsDbContext db, CancellationToken ct) =>
        await db.Scenes.AsNoTracking()
            .OrderBy(s => s.SortOrder)
            .Select(s => new SceneInfo(s.Id, s.Name, s.Icon))
            .ToListAsync(ct);

    private static async Task<IResult> ListScenes(RoomOsDbContext db, CancellationToken ct) =>
        Results.Ok(await ListAsync(db, ct));

    /// <summary>
    /// Démarre une scène et répond 202 immédiatement. La progression arrive par le
    /// hub, étape par étape : une scène peut durer une minute et demie si elle
    /// attend le réveil d'un PC.
    /// </summary>
    private static async Task<IResult> RunScene(
        string id, RoomOsDbContext db, SceneEngine engine, CancellationToken ct)
    {
        var scene = await db.Scenes.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, ct);

        if (scene is null)
        {
            return Results.NotFound(new { message = $"Scène « {id} » inconnue." });
        }

        SceneDefinition definition;

        try
        {
            definition = SceneDsl.Parse(scene.StepsJson);
        }
        catch (SceneDsl.InvalidSceneException ex)
        {
            // Une scène invalide en base est un bug de configuration, pas une erreur
            // utilisateur : on le dit clairement plutôt que de l'exécuter à moitié.
            return Results.Problem($"Scène « {id} » illisible : {ex.Message}");
        }

        var run = engine.Start(scene.Id, definition);

        return Results.Accepted(value: new { runId = run.RunId });
    }

    private static IResult GetRun(string runId, SceneEngine engine) =>
        engine.GetRun(runId) is { } run ? Results.Ok(run) : Results.NotFound();
}
