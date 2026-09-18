using Microsoft.Extensions.Logging.Abstractions;
using RoomOS.Core.Scenes;
using RoomOS.Domain.Contracts;
using Xunit;

namespace RoomOS.Core.Tests;

/// <summary>
/// Liste de cas imposée par docs/08-scenes.md. Le moteur de scènes est le seul
/// composant du projet qui mérite vraiment des tests unitaires : il orchestre des
/// effets de bord physiques, et ses règles d'échec ne se lisent pas dans le code
/// appelant.
/// </summary>
public sealed class SceneEngineTests
{
    /// <summary>Exécuteur d'essai : enregistre les appels, et peut échouer ou traîner.</summary>
    private sealed class FakeExecutor(string type) : IStepExecutor
    {
        public string Type { get; } = type;

        public List<SceneStep> Calls { get; } = [];

        public Exception? Throws { get; set; }

        public TimeSpan Delay { get; set; } = TimeSpan.Zero;

        public async Task ExecuteAsync(SceneStep step, CancellationToken ct)
        {
            Calls.Add(step);

            if (Delay > TimeSpan.Zero)
            {
                await Task.Delay(Delay, ct);
            }

            if (Throws is not null)
            {
                throw Throws;
            }
        }
    }

    private static SceneEngine NewEngine(params IStepExecutor[] executors) =>
        new(executors, TimeProvider.System, NullLogger<SceneEngine>.Instance);

    /// <summary>Attend la fin d'exécution : le moteur rend la main immédiatement.</summary>
    private static async Task<SceneRun> RunToEndAsync(
        SceneEngine engine, string sceneId, SceneDefinition definition)
    {
        var finished = new TaskCompletionSource<SceneFinished>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        engine.Finished += f => finished.TrySetResult(f);

        var run = engine.Start(sceneId, definition);

        await finished.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

        return engine.GetRun(run.RunId)!;
    }

    [Fact]
    public async Task Une_scene_vide_se_termine_en_completed()
    {
        var engine = NewEngine();

        var run = await RunToEndAsync(engine, "vide", new SceneDefinition([]));

        Assert.Equal(SceneRunStatus.Completed, run.Status);
        Assert.Empty(run.Steps);
    }

    [Fact]
    public async Task Une_etape_en_echec_n_empeche_pas_les_suivantes()
    {
        var failing = new FakeExecutor(SceneStepTypes.LightSet) { Throws = new InvalidOperationException("lampe absente") };
        var next = new FakeExecutor(SceneStepTypes.AudioSetVolume);

        var engine = NewEngine(failing, next);

        var run = await RunToEndAsync(engine, "chill", new SceneDefinition([
            new SceneStep(SceneStepTypes.LightSet, DeviceId: "ambient-light"),
            new SceneStep(SceneStepTypes.AudioSetVolume, DeviceId: "gaming-pc", Level: 35),
        ]));

        Assert.Equal(SceneRunStatus.Completed, run.Status);
        Assert.Equal(SceneStepStatus.Failed, run.Steps[0].Status);
        Assert.Equal(SceneStepStatus.Completed, run.Steps[1].Status);
        Assert.Single(next.Calls);
    }

    [Fact]
    public async Task Un_pc_wake_en_timeout_echoue_et_saute_les_etapes_suivantes()
    {
        var wake = new FakeExecutor(SceneStepTypes.PcWake) { Delay = TimeSpan.FromSeconds(30) };
        var after = new FakeExecutor(SceneStepTypes.AudioSetOutput);

        var engine = NewEngine(wake, after);

        var run = await RunToEndAsync(engine, "gaming", new SceneDefinition([
            new SceneStep(SceneStepTypes.PcWake, DeviceId: "gaming-pc", WaitForOnline: true, TimeoutSec: 1),
            new SceneStep(SceneStepTypes.AudioSetOutput, DeviceId: "gaming-pc", OutputId: "headset"),
        ]));

        Assert.Equal(SceneRunStatus.Failed, run.Status);
        Assert.Equal(SceneStepStatus.Failed, run.Steps[0].Status);
        Assert.Equal(SceneStepStatus.Skipped, run.Steps[1].Status);

        // L'étape suivante ne doit pas avoir été tentée : régler le volume d'un PC
        // éteint n'a pas de sens.
        Assert.Empty(after.Calls);
    }

    [Fact]
    public async Task Un_pc_wake_non_bloquant_n_interrompt_pas_la_scene()
    {
        var wake = new FakeExecutor(SceneStepTypes.PcWake) { Throws = new InvalidOperationException("WoL refusé") };
        var after = new FakeExecutor(SceneStepTypes.LightSet);

        var engine = NewEngine(wake, after);

        var run = await RunToEndAsync(engine, "work", new SceneDefinition([
            new SceneStep(SceneStepTypes.PcWake, DeviceId: "gaming-pc", WaitForOnline: false),
            new SceneStep(SceneStepTypes.LightSet, DeviceId: "desk-light", On: true),
        ]));

        Assert.Equal(SceneRunStatus.Completed, run.Status);
        Assert.Single(after.Calls);
    }

    [Fact]
    public async Task Une_nouvelle_scene_annule_celle_en_cours()
    {
        var slow = new FakeExecutor(SceneStepTypes.Delay) { Delay = TimeSpan.FromSeconds(5) };
        var quick = new FakeExecutor(SceneStepTypes.LightSet);

        var engine = NewEngine(slow, quick);

        var first = engine.Start("chill", new SceneDefinition([
            new SceneStep(SceneStepTypes.Delay, Ms: 5000),
            new SceneStep(SceneStepTypes.Delay, Ms: 5000),
        ]));

        // L'abonnement vient après le démarrage, avec l'identifiant déjà connu : les
        // deux scènes émettent « Finished », et la seconde est bien plus courte.
        // S'abonner avant obligerait à comparer à une variable pas encore affectée.
        var firstFinished = new TaskCompletionSource<SceneFinished>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        engine.Finished += f =>
        {
            if (f.RunId == first.RunId)
            {
                firstFinished.TrySetResult(f);
            }
        };

        // Une seule exécution à la fois par pièce : la seconde demande annule la première.
        engine.Start("night", new SceneDefinition([
            new SceneStep(SceneStepTypes.LightSet, DeviceId: "desk-light", On: false),
        ]));

        await firstFinished.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

        Assert.Equal(SceneRunStatus.Cancelled, engine.GetRun(first.RunId)!.Status);
    }

    [Fact]
    public async Task Chaque_type_d_etape_est_route_vers_son_executeur()
    {
        var executors = SceneStepTypes.All.Select(t => new FakeExecutor(t)).ToList();
        var engine = NewEngine([.. executors]);

        var steps = SceneStepTypes.All.Select(t => new SceneStep(t)).ToList();

        var run = await RunToEndAsync(engine, "tout", new SceneDefinition(steps));

        Assert.Equal(SceneRunStatus.Completed, run.Status);
        Assert.All(executors, e => Assert.Single(e.Calls));
    }

    /// <summary>
    /// Les exécutions vivent en mémoire. Sans plafond, un panneau utilisé tous les
    /// jours accumulerait sans fin.
    /// </summary>
    [Fact]
    public async Task Les_anciennes_executions_sont_purgees()
    {
        var engine = NewEngine();
        var ids = new List<string>();

        for (var i = 0; i < 25; i++)
        {
            var run = await RunToEndAsync(engine, $"scene-{i}", new SceneDefinition([]));
            ids.Add(run.RunId);
        }

        // Les 20 dernières restent consultables, les 5 premières ont disparu.
        Assert.All(ids.TakeLast(20), id => Assert.NotNull(engine.GetRun(id)));
        Assert.All(ids.Take(5), id => Assert.Null(engine.GetRun(id)));
    }

    [Fact]
    public async Task Un_type_sans_executeur_echoue_sans_faire_tomber_la_scene()
    {
        var engine = NewEngine();

        var run = await RunToEndAsync(engine, "orpheline", new SceneDefinition([
            new SceneStep(SceneStepTypes.MusicPlay),
        ]));

        Assert.Equal(SceneRunStatus.Completed, run.Status);
        Assert.Equal(SceneStepStatus.Failed, run.Steps[0].Status);
        Assert.Contains("exécuteur", run.Steps[0].Message);
    }
}
