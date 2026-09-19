using Microsoft.EntityFrameworkCore;
using RoomOS.Core.Auth;
using RoomOS.Core.Data;
using RoomOS.Core.Hubs;
using RoomOS.Core.Data.Entities;
using RoomOS.Core.Routines;
using RoomOS.Domain.Contracts;

namespace RoomOS.Core.Api;

public static class RoutineEndpoints
{
    public sealed record SaveRoutineRequest(
        string? Name, string? SceneId, string? Time, IReadOnlyList<bool>? Days, bool? Enabled);

    public static IEndpointRouteBuilder MapRoutineEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/routines")
            .RequireAuthorization(TokenAuthenticationHandler.ClientPolicy);

        group.MapGet("/", (RoomOsDbContext db, RoutineScheduler scheduler, CancellationToken ct) =>
            ListAsync(db, scheduler, ct));
        group.MapPost("/", Create);
        group.MapPut("/{id}", Save);
        group.MapDelete("/{id}", Delete);

        return app;
    }

    public static async Task<List<RoutineInfo>> ListAsync(
        RoomOsDbContext db, RoutineScheduler scheduler, CancellationToken ct)
    {
        var routines = await db.Routines.AsNoTracking().OrderBy(r => r.SortOrder).ToListAsync(ct);
        var scenes = await db.Scenes.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.Name, ct);

        return [.. routines.Select(r => new RoutineInfo(
            r.Id,
            r.Name,
            r.SceneId,
            scenes.GetValueOrDefault(r.SceneId, r.SceneId),
            $"{r.MinuteOfDay / 60:D2}:{r.MinuteOfDay % 60:D2}",
            [.. r.Days.Select(c => c == '1')],
            r.Enabled,
            scheduler.LastFired(r.Id)?.ToString("HH:mm")))];
    }

    private static async Task<IResult> Create(
        SaveRoutineRequest request, RoomOsDbContext db, CatalogNotifier notifier, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Results.BadRequest(new { message = "Une routine a besoin d'un nom." });
        }

        if (request.SceneId is not { } sceneId || !await db.Scenes.AnyAsync(s => s.Id == sceneId, ct))
        {
            return Results.BadRequest(new { message = "Il faut une scène existante." });
        }

        if (request.Time is not { } time || !TryParseMinutes(time, out var minutes))
        {
            return Results.BadRequest(new { message = "L'heure doit être au format HH:mm." });
        }

        var id = Guid.NewGuid().ToString("n")[..8];

        db.Routines.Add(new Routine
        {
            Id = id,
            Name = request.Name.Trim(),
            SceneId = sceneId,
            MinuteOfDay = minutes,
            Days = request.Days is { Count: 7 } d ? string.Concat(d.Select(x => x ? '1' : '0')) : "1111111",
            // Créée désactivée : on règle, on relit, puis on active.
            Enabled = false,
            SortOrder = await db.Routines.CountAsync(ct),
        });

        await db.SaveChangesAsync(ct);
        await notifier.PublishAsync(db, ct);

        return Results.Created($"/api/routines/{id}", new { id });
    }

    private static async Task<IResult> Delete(
        string id,
        RoomOsDbContext db,
        RoutineScheduler scheduler,
        CatalogNotifier notifier,
        CancellationToken ct)
    {
        var routine = await db.Routines.FirstOrDefaultAsync(r => r.Id == id, ct);

        if (routine is null)
        {
            return Results.NotFound(new { message = $"Routine « {id} » inconnue." });
        }

        db.Routines.Remove(routine);
        await db.SaveChangesAsync(ct);
        scheduler.Forget(id);
        await notifier.PublishAsync(db, ct);

        return Results.NoContent();
    }

    private static async Task<IResult> Save(
        string id,
        SaveRoutineRequest request,
        RoomOsDbContext db,
        RoutineScheduler scheduler,
        CatalogNotifier notifier,
        CancellationToken ct)
    {
        var routine = await db.Routines.FirstOrDefaultAsync(r => r.Id == id, ct);

        if (routine is null)
        {
            return Results.NotFound(new { message = $"Routine « {id} » inconnue." });
        }

        if (request.Time is { } time)
        {
            if (!TryParseMinutes(time, out var minutes))
            {
                return Results.BadRequest(new { message = "L'heure doit être au format HH:mm." });
            }

            routine.MinuteOfDay = minutes;
        }

        if (request.Days is { Count: 7 } days)
        {
            routine.Days = string.Concat(days.Select(d => d ? '1' : '0'));
        }
        else if (request.Days is not null)
        {
            return Results.BadRequest(new { message = "Il faut exactement sept jours." });
        }

        if (request.SceneId is { } sceneId)
        {
            if (!await db.Scenes.AnyAsync(s => s.Id == sceneId, ct))
            {
                return Results.BadRequest(new { message = $"Scène « {sceneId} » inconnue." });
            }

            routine.SceneId = sceneId;
        }

        if (request.Name is { } name && !string.IsNullOrWhiteSpace(name))
        {
            routine.Name = name.Trim();
        }

        if (request.Enabled is { } enabled)
        {
            routine.Enabled = enabled;
        }

        await db.SaveChangesAsync(ct);

        // Une routine modifiée oublie son dernier déclenchement : avancer l'heure
        // d'une routine déjà partie aujourd'hui doit pouvoir la faire repartir.
        scheduler.Forget(id);
        await notifier.PublishAsync(db, ct);

        return Results.Ok();
    }

    private static bool TryParseMinutes(string time, out int minutes)
    {
        minutes = 0;
        var parts = time.Split(':');

        if (parts.Length != 2
            || !int.TryParse(parts[0], out var h) || h is < 0 or > 23
            || !int.TryParse(parts[1], out var m) || m is < 0 or > 59)
        {
            return false;
        }

        minutes = (h * 60) + m;
        return true;
    }
}
