namespace RoomOS.Agent.Windows;

/// <summary>
/// Squelette M0 : le service démarre et s'arrête proprement, rien de plus.
/// La connexion SignalR, la télémétrie et l'audio arrivent en M1 et M2.
/// </summary>
public sealed class AgentWorker(ILogger<AgentWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Agent RoomOS démarré.");

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Agent RoomOS arrêté.");
        }
    }
}
