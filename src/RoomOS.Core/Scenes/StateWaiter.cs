using RoomOS.Core.State;

namespace RoomOS.Core.Scenes;

/// <summary>
/// Attend qu'un effet soit constaté dans le <see cref="StateStore"/>.
/// </summary>
/// <remarks>
/// <para>
/// <c>docs/03-architecture.md</c>, règle 5 : « le Core ne suppose jamais qu'une
/// commande a réussi : il attend le changement d'état correspondant, ou il échoue ».
/// Sans cela, une scène annonce « appliquée » alors qu'elle n'a fait qu'écrire sur
/// un socket — et un agent déconnecté, un pont MQTT muet ou une ampoule hors de
/// portée passeraient pour des succès.
/// </para>
/// <para>
/// Le sondage est préféré à l'abonnement aux événements du store : il n'y a pas de
/// course entre l'inscription et l'arrivée de l'état, et une condition déjà vraie
/// se résout immédiatement. Le coût est nul à cette échelle.
/// </para>
/// </remarks>
public static class StateWaiter
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(80);

    /// <summary>
    /// Rend la main dès que <paramref name="satisfied"/> est vrai. Lève
    /// <see cref="OperationCanceledException"/> si le délai de l'étape expire —
    /// le moteur la marque alors en échec, ce qui est le comportement voulu.
    /// </summary>
    public static async Task UntilAsync(Func<bool> satisfied, CancellationToken ct)
    {
        while (!satisfied())
        {
            await Task.Delay(PollInterval, ct);
        }
    }
}
