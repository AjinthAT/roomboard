using System.Text.Json;
using RoomOS.Domain.Contracts;

namespace RoomOS.Core.Scenes;

/// <summary>
/// Lecture du DSL de scènes stocké en base.
/// </summary>
/// <remarks>
/// Le parsing est une des trois zones où <c>docs/11-conventions.md</c> exige des
/// tests : un DSL mal lu produit une scène qui fait silencieusement autre chose que
/// ce qui est écrit.
/// </remarks>
public static class SceneDsl
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>Levée sur un DSL illisible. Jamais avalée : une scène invalide est un bug.</summary>
    public sealed class InvalidSceneException(string message) : Exception(message);

    public static SceneDefinition Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidSceneException("Définition de scène vide.");
        }

        SceneDefinition? definition;

        try
        {
            definition = JsonSerializer.Deserialize<SceneDefinition>(json, Options);
        }
        catch (JsonException ex)
        {
            throw new InvalidSceneException($"JSON invalide : {ex.Message}");
        }

        if (definition is null)
        {
            throw new InvalidSceneException("Définition de scène nulle.");
        }

        var steps = definition.Steps ?? [];

        for (var i = 0; i < steps.Count; i++)
        {
            var step = steps[i];

            if (string.IsNullOrWhiteSpace(step.Type))
            {
                throw new InvalidSceneException($"Étape {i} sans type.");
            }

            if (!SceneStepTypes.All.Contains(step.Type))
            {
                // Refuser à la lecture plutôt qu'ignorer à l'exécution : une faute de
                // frappe dans un type donnerait sinon une scène silencieusement amputée.
                throw new InvalidSceneException($"Étape {i} : type « {step.Type} » inconnu.");
            }
        }

        return new SceneDefinition(steps);
    }
}
