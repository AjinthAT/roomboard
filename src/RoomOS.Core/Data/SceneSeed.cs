using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RoomOS.Core.Configuration;
using RoomOS.Core.Data.Entities;
using RoomOS.Domain.Contracts;

namespace RoomOS.Core.Data;

/// <summary>
/// Les quatre scènes de <c>docs/08-scenes.md</c>, construites à partir de la
/// configuration réelle plutôt qu'écrites en dur.
/// </summary>
/// <remarks>
/// <para>
/// Les identifiants d'appareils viennent des options : une scène qui citerait
/// « desk-light » en dur casserait au premier renommage, et silencieusement — une
/// étape visant un appareil inconnu échoue sans interrompre la scène.
/// </para>
/// <para>
/// Les étapes visant un appareil non déclaré sont <strong>omises</strong>, pas
/// écrites puis vouées à l'échec. Tant qu'il n'y a qu'une ampoule, les scènes ne
/// parlent que d'elle.
/// </para>
/// </remarks>
public static class SceneSeed
{
    public static async Task SeedAsync(RoomOsDbContext db, RoomOsOptions options, CancellationToken ct)
    {
        var pc = options.Pc.Id;
        var lights = options.Lights.Select(l => l.Id).ToList();
        var desk = lights.ElementAtOrDefault(0);
        var ambient = lights.ElementAtOrDefault(1);

        var jbl = options.AudioOutputs.ElementAtOrDefault(0)?.Id ?? "jbl";
        var headset = options.AudioOutputs.ElementAtOrDefault(1)?.Id ?? "headset";

        var scenes = new List<(string Id, string Name, string Icon, List<SceneStep> Steps)>
        {
            ("work", "Work", "laptop",
            [
                new(SceneStepTypes.PcWake, DeviceId: pc, WaitForOnline: true, TimeoutSec: 90),
                new(SceneStepTypes.AudioSetOutput, DeviceId: pc, OutputId: jbl),
                new(SceneStepTypes.AudioSetVolume, DeviceId: pc, Level: 40),
                .. Light(desk, on: true, brightness: 100, mired: 160, transition: 1),
                .. Light(ambient, on: false, transition: 1),
            ]),

            ("chill", "Chill", "sofa",
            [
                .. Light(desk, on: true, brightness: 30, mired: 450, transition: 3),
                .. Light(ambient, on: true, brightness: 60, mired: 450, transition: 3),
                new(SceneStepTypes.AudioSetOutput, DeviceId: pc, OutputId: jbl),
                new(SceneStepTypes.AudioSetVolume, DeviceId: pc, Level: 35),
                new(SceneStepTypes.MusicTransfer),
                new(SceneStepTypes.MusicPlay),
            ]),

            ("gaming", "Gaming", "gamepad-2",
            [
                new(SceneStepTypes.PcWake, DeviceId: pc, WaitForOnline: true, TimeoutSec: 90),
                new(SceneStepTypes.AudioSetOutput, DeviceId: pc, OutputId: headset),
                new(SceneStepTypes.AudioSetVolume, DeviceId: pc, Level: 60),
                .. Light(desk, on: true, brightness: 20, color: "#FF4400", transition: 1),
                .. Light(ambient, on: false, transition: 1),
                new(SceneStepTypes.MusicTransfer),
            ]),

            // L'extinction vient en dernier : après pc.shutdown, l'appareil Spotify
            // disparaît et music.pause échouerait (docs/08-scenes.md).
            ("night", "Night", "moon",
            [
                new(SceneStepTypes.MusicPause),
                // Fondu long : la lampe s'éteint doucement plutôt que de claquer.
                .. Light(desk, on: false, transition: 5),
                .. Light(ambient, on: false, transition: 5),
                new(SceneStepTypes.PcShutdown, DeviceId: pc),
            ]),
        };

        for (var i = 0; i < scenes.Count; i++)
        {
            var (id, name, icon, steps) = scenes[i];
            var json = JsonSerializer.Serialize(new SceneDefinition(steps));

            var existing = await db.Scenes.FirstOrDefaultAsync(s => s.Id == id, ct);

            if (existing is null)
            {
                db.Scenes.Add(new Scene
                {
                    Id = id,
                    RoomId = DatabaseSeeder.RoomId,
                    Name = name,
                    Icon = icon,
                    StepsJson = json,
                    SortOrder = i,
                });
            }
            else
            {
                existing.Name = name;
                existing.Icon = icon;
                existing.StepsJson = json;
                existing.SortOrder = i;
            }
        }
    }

    /// <summary>Zéro ou une étape : une lampe non déclarée n'apparaît pas dans la scène.</summary>
    /// <remarks>
    /// La température de blanc est préférée à une couleur RVB quand la scène demande
    /// du blanc : une ampoule à blanc réglable la rend nettement mieux qu'une
    /// approximation, et 160 mireds donne un vrai froid là où <c>#F2F6FF</c> tirait
    /// au blafard.
    /// </remarks>
    private static IEnumerable<SceneStep> Light(
        string? deviceId,
        bool on,
        int? brightness = null,
        string? color = null,
        int? mired = null,
        double? transition = null)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            yield break;
        }

        yield return new SceneStep(
            SceneStepTypes.LightSet,
            DeviceId: deviceId,
            On: on,
            Brightness: on ? brightness : null,
            ColorHex: on ? color : null,
            ColorTempMired: on ? mired : null,
            TransitionSec: transition);
    }
}
