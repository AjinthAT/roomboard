using System.Collections.Concurrent;
using RoomOS.Domain.Contracts;

namespace RoomOS.Core.Scenes;

/// <summary>
/// Interprète une scène : exécution séquentielle, avec timeout par étape.
/// </summary>
/// <remarks>
/// Les règles viennent de <c>docs/08-scenes.md</c> :
/// séquentiel dans l'ordre déclaré ; timeout par étape, 10 s par défaut et 90 s pour
/// <c>pc.wake</c> ; une étape en échec n'interrompt pas la scène, <strong>sauf</strong>
/// <c>pc.wake</c> avec <c>waitForOnline</c>, car régler le volume d'un PC éteint n'a
/// pas de sens ; une seule exécution à la fois, une nouvelle demande annule la
/// précédente ; et relancer une scène déjà en place ne casse rien.
/// </remarks>
public sealed class SceneEngine
{
    public static readonly TimeSpan DefaultStepTimeout = TimeSpan.FromSeconds(10);
    public static readonly TimeSpan WakeStepTimeout = TimeSpan.FromSeconds(90);

    private readonly IReadOnlyDictionary<string, IStepExecutor> _executors;
    private readonly TimeProvider _time;
    private readonly ILogger<SceneEngine> _logger;

    /// <summary>
    /// Nombre d'exécutions conservées. <c>GET /api/scenes/runs/{runId}</c> sert à
    /// suivre une scène en cours ou tout juste finie, pas à tenir un historique :
    /// sans plafond, un panneau utilisé tous les jours accumule sans fin.
    /// </summary>
    private const int RetainedRuns = 20;

    private readonly ConcurrentDictionary<string, SceneRun> _runs = new();
    private readonly Lock _gate = new();

    private CancellationTokenSource? _current;

    public SceneEngine(
        IEnumerable<IStepExecutor> executors,
        TimeProvider time,
        ILogger<SceneEngine> logger)
    {
        _executors = executors.ToDictionary(e => e.Type, StringComparer.Ordinal);
        _time = time;
        _logger = logger;
    }

    public event Action<SceneStarted>? Started;
    public event Action<SceneStepCompleted>? StepCompleted;
    public event Action<SceneFinished>? Finished;

    public SceneRun? GetRun(string runId) => _runs.TryGetValue(runId, out var run) ? run : null;

    /// <summary>
    /// Démarre une scène et rend la main immédiatement. La progression est poussée
    /// sur le hub client, étape par étape.
    /// </summary>
    public SceneRun Start(string sceneId, SceneDefinition definition)
    {
        var runId = Guid.NewGuid().ToString("n");
        CancellationTokenSource cts;

        lock (_gate)
        {
            // Une seule exécution à la fois : la précédente est annulée, pas mise en
            // file. Appuyer sur « Gaming » puis « Night » doit donner Night.
            _current?.Cancel();
            cts = new CancellationTokenSource();
            _current = cts;
        }

        var run = new SceneRun(runId, sceneId, SceneRunStatus.Running, [], _time.GetUtcNow());
        _runs[runId] = run;
        Trim();

        Started?.Invoke(new SceneStarted(runId, sceneId));

        _ = RunAsync(runId, definition, cts);

        return run;
    }

    private async Task RunAsync(string runId, SceneDefinition definition, CancellationTokenSource cts)
    {
        var results = new List<SceneStepResult>();
        var status = SceneRunStatus.Completed;

        try
        {
            for (var index = 0; index < definition.Steps.Count; index++)
            {
                if (cts.IsCancellationRequested)
                {
                    status = SceneRunStatus.Cancelled;
                    AppendSkipped(results, definition, index, "Scène annulée.");
                    break;
                }

                var step = definition.Steps[index];
                var result = await ExecuteStepAsync(index, step, cts.Token);

                results.Add(result);
                Publish(runId, results, result);

                // Seul pc.wake bloquant interrompt : les étapes suivantes visent un PC
                // qui n'est pas là.
                if (result.Status == SceneStepStatus.Failed && IsBlocking(step))
                {
                    status = SceneRunStatus.Failed;
                    AppendSkipped(results, definition, index + 1, "Étape bloquante en échec.");
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            status = SceneRunStatus.Cancelled;
        }
        finally
        {
            // Libérer le jeton sous verrou avant de le disposer. Dans l'ordre inverse,
            // une scène lancée entre les deux prend le verrou, voit encore ce jeton et
            // appelle Cancel() sur un objet déjà libéré — ObjectDisposedException, donc
            // 500 sur l'API, précisément quand on enchaîne deux scènes.
            lock (_gate)
            {
                if (ReferenceEquals(_current, cts))
                {
                    _current = null;
                }
            }

            cts.Dispose();
        }

        Update(runId, run => run with { Status = status, Steps = results });
        Finished?.Invoke(new SceneFinished(runId, status.ToString()));

        _logger.LogInformation("Scène {RunId} terminée : {Status}.", runId, status);
    }

    private async Task<SceneStepResult> ExecuteStepAsync(int index, SceneStep step, CancellationToken ct)
    {
        if (!_executors.TryGetValue(step.Type, out var executor))
        {
            return new SceneStepResult(index, step.Type, SceneStepStatus.Failed, "Aucun exécuteur.");
        }

        var timeout = step.Type == SceneStepTypes.PcWake
            ? TimeSpan.FromSeconds(step.TimeoutSec ?? (int)WakeStepTimeout.TotalSeconds)
            : DefaultStepTimeout;

        using var timed = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timed.CancelAfter(timeout);

        try
        {
            await executor.ExecuteAsync(step, timed.Token);
            return new SceneStepResult(index, step.Type, SceneStepStatus.Completed, null);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return new SceneStepResult(
                index, step.Type, SceneStepStatus.Failed, $"Dépassement de {timeout.TotalSeconds:0} s.");
        }
        catch (Exception ex)
        {
            return new SceneStepResult(index, step.Type, SceneStepStatus.Failed, ex.Message);
        }
    }

    private static bool IsBlocking(SceneStep step) =>
        step.Type == SceneStepTypes.PcWake && step.WaitForOnline;

    private static void AppendSkipped(
        List<SceneStepResult> results, SceneDefinition definition, int from, string reason)
    {
        for (var i = from; i < definition.Steps.Count; i++)
        {
            results.Add(new SceneStepResult(
                i, definition.Steps[i].Type, SceneStepStatus.Skipped, reason));
        }
    }

    /// <summary>
    /// Met à jour une exécution si elle est encore conservée.
    /// </summary>
    /// <remarks>
    /// Une scène très longue peut être purgée avant de finir, si beaucoup d'autres
    /// démarrent entre-temps. L'indexeur lèverait alors <c>KeyNotFoundException</c>
    /// et ferait tomber la scène en cours d'exécution : on ignore silencieusement,
    /// c'est une trace perdue, pas un effet manqué.
    /// </remarks>
    private void Update(string runId, Func<SceneRun, SceneRun> change)
    {
        if (_runs.TryGetValue(runId, out var run))
        {
            _runs[runId] = change(run);
        }
    }

    /// <summary>Ne garde que les exécutions les plus récentes.</summary>
    private void Trim()
    {
        if (_runs.Count <= RetainedRuns)
        {
            return;
        }

        foreach (var old in _runs.Values
            .OrderByDescending(r => r.StartedAt)
            .Skip(RetainedRuns)
            .ToList())
        {
            _runs.TryRemove(old.RunId, out _);
        }
    }

    private void Publish(string runId, List<SceneStepResult> results, SceneStepResult result)
    {
        Update(runId, run => run with { Steps = [.. results] });

        StepCompleted?.Invoke(new SceneStepCompleted(
            runId, result.Index, result.Status.ToString(), result.Message));
    }
}
