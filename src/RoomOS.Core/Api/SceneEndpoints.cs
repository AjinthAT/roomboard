using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RoomOS.Core.Auth;
using RoomOS.Core.Data.Entities;
using RoomOS.Core.Data;
using RoomOS.Core.Hubs;
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
        group.MapGet("/{id}", GetScene);
        group.MapPost("/", CreateScene);
        group.MapPut("/{id}", UpdateScene);
        group.MapDelete("/{id}", DeleteScene);
        group.MapPost("/{id}/run", RunScene);
        group.MapGet("/runs/{runId}", GetRun);

        return app;
    }

    public static async Task<List<SceneInfo>> ListAsync(RoomOsDbContext db, CancellationToken ct)
    {
        var scenes = await db.Scenes.AsNoTracking()
            .OrderBy(s => s.SortOrder)
            .ToListAsync(ct);

        return [.. scenes.Select(s => new SceneInfo(s.Id, s.Name, s.Icon, IsDestructive(s.StepsJson)))];
    }

    /// <summary>
    /// Une scène est sensible si elle éteint un PC. Un DSL illisible est traité comme
    /// sensible : dans le doute, on demande confirmation.
    /// </summary>
    private static bool IsDestructive(string stepsJson)
    {
        try
        {
            return SceneDsl.Parse(stepsJson).Steps.Any(s => s.Type == SceneStepTypes.PcShutdown);
        }
        catch (SceneDsl.InvalidSceneException)
        {
            return true;
        }
    }

    private static async Task<IResult> ListScenes(RoomOsDbContext db, CancellationToken ct) =>
        Results.Ok(await ListAsync(db, ct));

    public sealed record SceneDetail(
        string Id, string Name, string Icon, IReadOnlyList<SceneStep> Steps);

    public sealed record SaveSceneRequest(
        string? Name, string? Icon, IReadOnlyList<SceneStep>? Steps);

    /// <summary>Une scène avec ses étapes, pour l'éditeur.</summary>
    private static async Task<IResult> GetScene(
        string id, RoomOsDbContext db, CancellationToken ct)
    {
        var scene = await db.Scenes.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, ct);

        if (scene is null)
        {
            return Results.NotFound(new { message = $"Scène « {id} » inconnue." });
        }

        try
        {
            return Results.Ok(new SceneDetail(
                scene.Id, scene.Name, scene.Icon, SceneDsl.Parse(scene.StepsJson).Steps));
        }
        catch (SceneDsl.InvalidSceneException ex)
        {
            return Results.Problem($"Scène « {id} » illisible : {ex.Message}");
        }
    }

    private static async Task<IResult> CreateScene(
        SaveSceneRequest request, RoomOsDbContext db, CatalogNotifier notifier, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Results.BadRequest(new { message = "Une scène a besoin d'un nom." });
        }

        var id = Slug(request.Name);

        if (await db.Scenes.AnyAsync(s => s.Id == id, ct))
        {
            return Results.Conflict(new { message = $"Une scène « {request.Name} » existe déjà." });
        }

        var steps = request.Steps ?? [];

        if (await ValidateAsync(steps, db, ct) is { } error)
        {
            return Results.BadRequest(new { message = error });
        }

        var room = await db.Rooms.AsNoTracking().FirstOrDefaultAsync(ct);

        if (room is null)
        {
            return Results.Problem("Aucune pièce en base.");
        }

        var order = await db.Scenes.CountAsync(ct);

        db.Scenes.Add(new Scene
        {
            Id = id,
            RoomId = room.Id,
            Name = request.Name.Trim(),
            Icon = string.IsNullOrWhiteSpace(request.Icon) ? "sparkles" : request.Icon,
            StepsJson = JsonSerializer.Serialize(new SceneDefinition(steps)),
            SortOrder = order,
        });

        await db.SaveChangesAsync(ct);
        await notifier.PublishAsync(db, ct);

        return Results.Created($"/api/scenes/{id}", new { id });
    }

    private static async Task<IResult> UpdateScene(
        string id,
        SaveSceneRequest request,
        RoomOsDbContext db,
        CatalogNotifier notifier,
        CancellationToken ct)
    {
        var scene = await db.Scenes.FirstOrDefaultAsync(s => s.Id == id, ct);

        if (scene is null)
        {
            return Results.NotFound(new { message = $"Scène « {id} » inconnue." });
        }

        if (request.Steps is { } steps)
        {
            if (await ValidateAsync(steps, db, ct) is { } error)
            {
                return Results.BadRequest(new { message = error });
            }

            scene.StepsJson = JsonSerializer.Serialize(new SceneDefinition(steps));
        }

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            scene.Name = request.Name.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.Icon))
        {
            scene.Icon = request.Icon;
        }

        await db.SaveChangesAsync(ct);
        await notifier.PublishAsync(db, ct);

        return Results.Ok();
    }

    private static async Task<IResult> DeleteScene(
        string id, RoomOsDbContext db, CatalogNotifier notifier, CancellationToken ct)
    {
        var scene = await db.Scenes.FirstOrDefaultAsync(s => s.Id == id, ct);

        if (scene is null)
        {
            return Results.NotFound(new { message = $"Scène « {id} » inconnue." });
        }

        // Une routine qui pointe vers une scène supprimée ne se déclencherait jamais,
        // en silence. On refuse plutôt que de laisser une automatisation morte.
        var used = await db.Routines.Where(r => r.SceneId == id).Select(r => r.Name).ToListAsync(ct);

        if (used.Count > 0)
        {
            return Results.Conflict(new
            {
                message = $"Utilisée par : {string.Join(", ", used)}. Change ces routines d'abord.",
            });
        }

        db.Scenes.Remove(scene);
        await db.SaveChangesAsync(ct);
        await notifier.PublishAsync(db, ct);

        return Results.NoContent();
    }

    /// <summary>Étapes qui visent un appareil, et le type que cet appareil doit avoir.</summary>
    private static readonly Dictionary<string, DeviceKind> NeedsDevice = new(StringComparer.Ordinal)
    {
        [SceneStepTypes.PcWake] = DeviceKind.Pc,
        [SceneStepTypes.PcShutdown] = DeviceKind.Pc,
        [SceneStepTypes.AudioSetOutput] = DeviceKind.Pc,
        [SceneStepTypes.AudioSetVolume] = DeviceKind.Pc,
        [SceneStepTypes.AudioSetMute] = DeviceKind.Pc,
        [SceneStepTypes.LightSet] = DeviceKind.Light,
    };

    /// <summary>
    /// Validation à l'écriture plutôt qu'à l'exécution : une scène enregistrée avec
    /// un type inconnu échouerait une étape sur deux sans qu'on sache pourquoi.
    /// </summary>
    private static async Task<string?> ValidateAsync(
        IReadOnlyList<SceneStep> steps, RoomOsDbContext db, CancellationToken ct)
    {
        if (steps.Count > 50)
        {
            return "Une scène est limitée à 50 étapes.";
        }

        var devices = await db.Devices.AsNoTracking()
            .ToDictionaryAsync(d => d.Id, d => d.Kind, ct);

        for (var i = 0; i < steps.Count; i++)
        {
            var step = steps[i];

            if (!SceneStepTypes.All.Contains(step.Type))
            {
                return $"Étape {i + 1} : type « {step.Type} » inconnu.";
            }

            if (step.Type == SceneStepTypes.AudioSetOutput && string.IsNullOrWhiteSpace(step.OutputId))
            {
                return $"Étape {i + 1} : il manque la sortie audio.";
            }

            if (!NeedsDevice.TryGetValue(step.Type, out var kind))
            {
                continue;
            }

            // Une étape sans cible, ou pointant vers un appareil disparu, s'enregistre
            // sans bruit puis échoue à l'exécution — le soir, sans personne devant un
            // journal. On la refuse à l'écriture.
            if (string.IsNullOrWhiteSpace(step.DeviceId))
            {
                return $"Étape {i + 1} : il manque l'appareil.";
            }

            if (!devices.TryGetValue(step.DeviceId, out var actual))
            {
                return $"Étape {i + 1} : appareil « {step.DeviceId} » inconnu.";
            }

            if (actual != kind)
            {
                return $"Étape {i + 1} : « {step.DeviceId} » n'est pas " +
                    (kind == DeviceKind.Light ? "une lampe." : "un PC.");
            }
        }

        return null;
    }

    /// <summary>
    /// Translittération des lettres accentuées. `string.Normalize` lèverait ici :
    /// le Core tourne en mode globalisation invariante (docs/02-stack.md), où la
    /// normalisation Unicode n'est pas disponible hors ASCII.
    /// </summary>
    private static char Deaccent(char c) => c switch
    {
        'à' or 'â' or 'ä' or 'á' or 'ã' or 'å' => 'a',
        'ç' => 'c',
        'è' or 'é' or 'ê' or 'ë' => 'e',
        'î' or 'ï' or 'í' or 'ì' => 'i',
        'ô' or 'ö' or 'ó' or 'ò' or 'õ' => 'o',
        'ù' or 'û' or 'ü' or 'ú' => 'u',
        'ÿ' or 'ý' => 'y',
        'ñ' => 'n',
        _ => c,
    };

    /// <summary>Identifiant lisible dérivé du nom, pour que l'URL et les journaux parlent.</summary>
    private static string Slug(string name)
    {
        var cleaned = new string([.. name.Trim().ToLowerInvariant()
            .Replace("œ", "oe").Replace("æ", "ae")
            .Select(Deaccent)
            .Select(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' ? c : '-')]);

        var slug = string.Join('-', cleaned.Split('-', StringSplitOptions.RemoveEmptyEntries));

        return string.IsNullOrEmpty(slug) ? Guid.NewGuid().ToString("n")[..8] : slug[..Math.Min(40, slug.Length)];
    }

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
