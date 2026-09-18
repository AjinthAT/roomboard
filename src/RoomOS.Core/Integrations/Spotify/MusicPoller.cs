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

    private async Task<bool> PollAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var provider = scope.ServiceProvider.GetRequiredService<IMusicProvider>();

            var music = await provider.GetStateAsync(ct);
            state.SetMusic(music);

            return music.NowPlaying.IsPlaying;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception ex)
        {
            // Spotify indisponible ne doit pas arrêter le sondage : on réessaiera au
            // rythme lent, sans bruit dans les journaux.
            logger.LogDebug(ex, "Sondage Spotify en échec.");
            return false;
        }
    }
}
