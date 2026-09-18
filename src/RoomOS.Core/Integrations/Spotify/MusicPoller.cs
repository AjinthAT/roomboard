using RoomOS.Core.State;
using RoomOS.Domain.Contracts;

namespace RoomOS.Core.Integrations.Spotify;

/// <summary>
/// Sonde Spotify et alimente le <see cref="StateStore"/>.
/// </summary>
/// <remarks>
/// Il n'existe pas de webhook côté Spotify : le sondage est la seule option
/// (docs/09-integrations.md). Il est adaptatif — 3 s quand une lecture est en cours,
/// 15 s sinon — parce qu'un dashboard allumé en permanence qui interrogerait une API
/// tierce toutes les 3 secondes jour et nuit est un bon moyen de se faire limiter.
/// </remarks>
public sealed class MusicPoller(
    IServiceScopeFactory scopeFactory,
    StateStore state,
    ILogger<MusicPoller> logger) : BackgroundService
{
    private static readonly TimeSpan WhilePlaying = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan WhileIdle = TimeSpan.FromSeconds(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var playing = await PollAsync(stoppingToken);

            try
            {
                await Task.Delay(playing ? WhilePlaying : WhileIdle, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    /// <summary>
    /// Vrai tant que le dernier sondage a échoué. Sert à ne journaliser qu'à la
    /// bascule : une panne de Spotify produirait sinon une ligne toutes les 15 s,
    /// indéfiniment.
    /// </summary>
    private bool _failing;

    private async Task<bool> PollAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var provider = scope.ServiceProvider.GetRequiredService<IMusicProvider>();

            var music = await provider.GetStateAsync(ct);
            state.SetMusic(music);

            if (_failing)
            {
                _failing = false;
                logger.LogInformation("Spotify répond à nouveau.");
            }

            return music.NowPlaying.IsPlaying;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception ex)
        {
            // Une panne de Spotify n'arrête pas le sondage, mais elle ne doit pas être
            // muette non plus : journaliser en Debug la rendait invisible au niveau
            // par défaut, ce qui revient à avaler l'exception.
            //
            // L'état connu du StateStore est conservé : mieux vaut un affichage
            // légèrement périmé qu'une carte vidée à chaque hoquet réseau.
            if (!_failing)
            {
                _failing = true;
                logger.LogWarning(ex, "Sondage Spotify en échec, tentatives poursuivies.");
            }

            return false;
        }
    }
}
