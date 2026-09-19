using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RoomOS.Core.Configuration;
using RoomOS.Core.Data;
using RoomOS.Core.Scenes;

namespace RoomOS.Core.Routines;

/// <summary>
/// Déclenche les scènes à l'heure dite.
/// </summary>
/// <remarks>
/// <para>
/// Le fuseau est explicite plutôt qu'hérité du conteneur, qui tourne en UTC : une
/// routine réglée sur 7 h partirait sinon à 9 h l'été. Il suit aussi les changements
/// d'heure tout seul, ce qu'un décalage fixe ne ferait pas.
/// </para>
/// <para>
/// Le dernier déclenchement est gardé en mémoire, pas en base. Un redémarrage du Core
/// pile à la minute d'une routine la rejouerait — sans conséquence, puisqu'une scène
/// est idempotente par construction (docs/08-scenes.md, règle 6). Persister cette
/// date coûterait une écriture par minute pour couvrir un cas sans dommage.
/// </para>
/// </remarks>
public sealed class RoutineScheduler(
    IServiceScopeFactory scopeFactory,
    SceneEngine engine,
    IOptions<RoomOsOptions> options,
    TimeProvider time,
    ILogger<RoutineScheduler> logger) : BackgroundService
{
    /// <summary>Assez fin pour une précision à la minute, assez lâche pour ne rien coûter.</summary>
    private static readonly TimeSpan Tick = TimeSpan.FromSeconds(20);

    private readonly ConcurrentDictionary<string, DateOnly> _firedOn = new();

    private TimeZoneInfo? _zone;

    public event Action<RoutineFiredEvent>? Fired;

    public sealed record RoutineFiredEvent(string RoutineId, string SceneId, string RunId);

    /// <summary>Heure locale courante, selon le fuseau configuré.</summary>
    public DateTimeOffset LocalNow()
    {
        _zone ??= ResolveZone(options.Value.TimeZone, logger);
        return TimeZoneInfo.ConvertTime(time.GetUtcNow(), _zone);
    }

    private static TimeZoneInfo ResolveZone(string id, ILogger logger)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (Exception ex)
        {
            // Retomber en UTC plutôt que refuser de démarrer : les routines seront
            // décalées, le reste du Core fonctionne.
            logger.LogError(ex, "Fuseau « {Zone} » inconnu, repli sur UTC.", id);
            return TimeZoneInfo.Utc;
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Tick);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAsync(stoppingToken);
                await timer.WaitForNextTickAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Échec d'un passage du planificateur, on continue.");
            }
        }
    }

    private async Task CheckAsync(CancellationToken ct)
    {
        var now = LocalNow();
        var today = DateOnly.FromDateTime(now.DateTime);
        var minute = (now.Hour * 60) + now.Minute;

        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RoomOsDbContext>();

        var due = await db.Routines.AsNoTracking()
            .Where(r => r.Enabled && r.MinuteOfDay == minute)
            .ToListAsync(ct);

        foreach (var routine in due)
        {
            if (!RunsToday(routine.Days, now.DayOfWeek))
            {
                continue;
            }

            // Une routine ne part qu'une fois par jour : le planificateur repasse
            // trois fois par minute.
            if (_firedOn.TryGetValue(routine.Id, out var last) && last == today)
            {
                continue;
            }

            _firedOn[routine.Id] = today;

            var scene = await db.Scenes.AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == routine.SceneId, ct);

            if (scene is null)
            {
                logger.LogWarning(
                    "Routine {Routine} : scène {Scene} introuvable.", routine.Id, routine.SceneId);
                continue;
            }

            try
            {
                var run = engine.Start(scene.Id, SceneDsl.Parse(scene.StepsJson));

                logger.LogInformation(
                    "Routine {Routine} déclenchée à {Time} : scène {Scene}.",
                    routine.Id, now.ToString("HH:mm"), scene.Id);

                Fired?.Invoke(new RoutineFiredEvent(routine.Id, scene.Id, run.RunId));
            }
            catch (SceneDsl.InvalidSceneException ex)
            {
                logger.LogError(ex, "Routine {Routine} : scène illisible.", routine.Id);
            }
        }
    }

    /// <summary>Le masque va de lundi à dimanche, <see cref="DayOfWeek"/> part du dimanche.</summary>
    public static bool RunsToday(string days, DayOfWeek day)
    {
        if (days.Length != 7)
        {
            return false;
        }

        var index = day == DayOfWeek.Sunday ? 6 : (int)day - 1;
        return days[index] == '1';
    }

    /// <summary>Marque une routine comme déjà partie aujourd'hui, ou l'oublie.</summary>
    public void Forget(string routineId) => _firedOn.TryRemove(routineId, out _);
}
