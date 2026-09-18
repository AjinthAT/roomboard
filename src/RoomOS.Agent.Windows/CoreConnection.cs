using Microsoft.AspNetCore.SignalR.Client;

namespace RoomOS.Agent.Windows;

/// <summary>
/// Politique de reconnexion : backoff exponentiel plafonné à 30 s, indéfiniment.
/// L'agent doit survivre à un redémarrage du Core sans intervention
/// (docs/06-agent-windows.md).
/// </summary>
public sealed class CappedBackoffRetryPolicy : IRetryPolicy
{
    private static readonly TimeSpan Cap = TimeSpan.FromSeconds(30);

    public TimeSpan? NextRetryDelay(RetryContext retryContext)
    {
        var seconds = Math.Min(Math.Pow(2, retryContext.PreviousRetryCount), Cap.TotalSeconds);
        return TimeSpan.FromSeconds(seconds);
    }
}
