using System.Diagnostics;
using System.Reflection;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Options;
using RoomOS.Domain.Contracts;

namespace RoomOS.Agent.Windows;

/// <summary>
/// Maintient la connexion sortante vers le Core, publie la télémétrie et exécute
/// les commandes. L'agent est un exécutant : aucune logique métier ici.
/// </summary>
public sealed class AgentWorker(
    IOptions<AgentOptions> agentOptions,
    IOptions<TelemetryOptions> telemetryOptions,
    TelemetryReader telemetry,
    ILogger<AgentWorker> logger) : BackgroundService
{
    private readonly AgentOptions _options = agentOptions.Value;
    private readonly TimeSpan _interval = TimeSpan.FromMilliseconds(telemetryOptions.Value.IntervalMs);

    private static string Version =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(_options.AgentToken))
        {
            logger.LogCritical("Core:AgentToken n'est pas configuré. Arrêt.");
            return;
        }

        telemetry.TryOpen();

        await using var connection = Build();

        connection.On<AgentCommand>(AgentProtocol.ToAgent.Shutdown,
            command => ExecutePowerCommandAsync(connection, command, "/s"));

        connection.On<AgentCommand>(AgentProtocol.ToAgent.Restart,
            command => ExecutePowerCommandAsync(connection, command, "/r"));

        connection.Reconnected += async _ =>
        {
            logger.LogInformation("Reconnecté au Core.");
            await RegisterAsync(connection, stoppingToken);
        };

        connection.Closed += error =>
        {
            logger.LogWarning(error, "Connexion au Core fermée.");
            return Task.CompletedTask;
        };

        await ConnectAsync(connection, stoppingToken);
        await PublishTelemetryLoopAsync(connection, stoppingToken);
    }

    private HubConnection Build() =>
        new HubConnectionBuilder()
            .WithUrl($"{_options.Url.TrimEnd('/')}/hub/agent", http =>
            {
                http.AccessTokenProvider = () => Task.FromResult<string?>(_options.AgentToken);
            })
            .WithAutomaticReconnect(new CappedBackoffRetryPolicy())
            .Build();

    /// <summary>
    /// La reconnexion automatique ne couvre pas la connexion initiale : si le Core
    /// n'est pas encore démarré, il faut réessayer soi-même.
    /// </summary>
    private async Task ConnectAsync(HubConnection connection, CancellationToken ct)
    {
        var attempt = 0;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await connection.StartAsync(ct);
                await RegisterAsync(connection, ct);
                logger.LogInformation("Connecté au Core sur {Url}.", _options.Url);
                return;
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                var delay = TimeSpan.FromSeconds(Math.Min(Math.Pow(2, attempt++), 30));
                logger.LogWarning(ex, "Connexion au Core impossible, nouvel essai dans {Delay}.", delay);
                await Task.Delay(delay, ct);
            }
        }
    }

    private Task RegisterAsync(HubConnection connection, CancellationToken ct) =>
        connection.InvokeAsync(
            AgentProtocol.ToCore.Register,
            new RegisterRequest(_options.PcId, Version, Environment.TickCount64 / 1000),
            ct);

    private async Task PublishTelemetryLoopAsync(HubConnection connection, CancellationToken ct)
    {
        using var timer = new PeriodicTimer(_interval);

        while (await SafeWaitAsync(timer, ct))
        {
            if (connection.State != HubConnectionState.Connected)
            {
                continue;
            }

            try
            {
                await connection.InvokeAsync(
                    AgentProtocol.ToCore.PushTelemetry, _options.PcId, telemetry.Read(), ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Échec de publication de la télémétrie.");
            }
        }
    }

    private static async Task<bool> SafeWaitAsync(PeriodicTimer timer, CancellationToken ct)
    {
        try
        {
            return await timer.WaitForNextTickAsync(ct);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    /// <summary>
    /// Acquitte <strong>avant</strong> d'exécuter : après un <c>shutdown /s /t 0</c>,
    /// plus rien ne part (docs/06-agent-windows.md).
    /// </summary>
    private async Task ExecutePowerCommandAsync(
        HubConnection connection, AgentCommand command, string flag)
    {
        try
        {
            await connection.InvokeAsync(
                AgentProtocol.ToCore.Ack, new CommandAck(command.CommandId, "accepted", null));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Impossible d'acquitter {CommandId}.", command.CommandId);
        }

        logger.LogInformation("Exécution de shutdown {Flag} /t 0.", flag);

        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "shutdown",
            Arguments = $"{flag} /t 0",
            CreateNoWindow = true,
            UseShellExecute = false,
        });

        if (process is null)
        {
            logger.LogError("Le processus shutdown n'a pas démarré.");
        }
    }
}
