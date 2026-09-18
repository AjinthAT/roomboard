using RoomOS.Core.Scenes;
using RoomOS.Domain.Contracts;
using Xunit;

namespace RoomOS.Core.Tests;

/// <summary>
/// Parsing du DSL de scènes, exigé par docs/11-conventions.md. Un DSL mal lu produit
/// une scène qui fait silencieusement autre chose que ce qui est écrit.
/// </summary>
public sealed class SceneDslTests
{
    [Fact]
    public void Une_scene_complete_est_lue_champ_par_champ()
    {
        var definition = SceneDsl.Parse("""
        {
          "steps": [
            { "type": "pc.wake", "deviceId": "gaming-pc", "waitForOnline": true, "timeoutSec": 90 },
            { "type": "audio.setOutput", "deviceId": "gaming-pc", "outputId": "headset" },
            { "type": "light.set", "deviceId": "desk-light", "on": true, "brightness": 20, "colorHex": "#FF4400" },
            { "type": "music.transfer" },
            { "type": "delay", "ms": 1500 }
          ]
        }
        """);

        Assert.Equal(5, definition.Steps.Count);

        var wake = definition.Steps[0];
        Assert.Equal(SceneStepTypes.PcWake, wake.Type);
        Assert.Equal("gaming-pc", wake.DeviceId);
        Assert.True(wake.WaitForOnline);
        Assert.Equal(90, wake.TimeoutSec);

        Assert.Equal("headset", definition.Steps[1].OutputId);

        var light = definition.Steps[2];
        Assert.True(light.On);
        Assert.Equal(20, light.Brightness);
        Assert.Equal("#FF4400", light.ColorHex);

        Assert.Equal(1500, definition.Steps[4].Ms);
    }

    [Fact]
    public void Une_scene_sans_etape_est_valide()
    {
        Assert.Empty(SceneDsl.Parse("""{ "steps": [] }""").Steps);
    }

    /// <summary>
    /// Une faute de frappe dans un type doit être refusée à la lecture. L'ignorer à
    /// l'exécution donnerait une scène silencieusement amputée d'une étape.
    /// </summary>
    [Fact]
    public void Un_type_inconnu_est_refuse()
    {
        var ex = Assert.Throws<SceneDsl.InvalidSceneException>(
            () => SceneDsl.Parse("""{ "steps": [ { "type": "light.sett" } ] }"""));

        Assert.Contains("light.sett", ex.Message);
    }

    [Fact]
    public void Une_etape_sans_type_est_refusee()
    {
        Assert.Throws<SceneDsl.InvalidSceneException>(
            () => SceneDsl.Parse("""{ "steps": [ { "deviceId": "desk-light" } ] }"""));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("pas du json")]
    [InlineData("{ \"steps\": ")]
    public void Un_json_illisible_est_refuse(string json)
    {
        Assert.Throws<SceneDsl.InvalidSceneException>(() => SceneDsl.Parse(json));
    }

    [Fact]
    public void La_casse_des_noms_de_champs_est_toleree()
    {
        var definition = SceneDsl.Parse("""{ "Steps": [ { "Type": "delay", "MS": 100 } ] }""");

        Assert.Equal(100, definition.Steps[0].Ms);
    }

    [Fact]
    public void Tous_les_types_declares_sont_acceptes()
    {
        foreach (var type in SceneStepTypes.All)
        {
            var definition = SceneDsl.Parse($$"""{ "steps": [ { "type": "{{type}}" } ] }""");
            Assert.Equal(type, definition.Steps[0].Type);
        }
    }
}
