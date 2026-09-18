using System.Diagnostics;
using System.Reflection;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Options;
using RoomOS.Agent.Windows.Audio;
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
    AudioController audio,
    ILogger<AgentWorker> logger) : BackgroundService
{
    /// <summary>Filet : republier l'état audio même sans changement, au cas où un
    /// message se serait perdu.</summary>
    private static readonly TimeSpan AudioHeartbeat = TimeSpan.FromSeconds(10);

    private readonly AgentOptions _options = agentOptions.Value;
    private readonly TimeSpan _interval = TimeSpan.FromMilliseconds(telemetryOptions.Value.IntervalMs);

    private AgentAudioState? _lastAudio;
    private DateTimeOffset _lastAudioPush = DateTimeOffset.MinValue;

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

        connection.On<SetAudioOutputCommand>(AgentProtocol.ToAgent.SetAudioOutput,
            command => AcknowledgeAsync(
                connection, command.CommandId, audio.TrySetOutput(command.WindowsDeviceId)));

        connection.On<SetVolumeCommand>(AgentProtocol.ToAgent.SetVolume,
            command => AcknowledgeAsync(
                connection, command.CommandId, audio.TrySetVolume(command.Level)));

        connection.On<SetMuteCommand>(AgentProtocol.ToAgent.SetMute,
            command => AcknowledgeAsync(
                connection, command.CommandId, audio.TrySetMute(command.Muted)));

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
                    AgentProtocol.ToCore.PushTelemetry, telemetry.Read(), ct);

                await PublishAudioIfNeededAsync(connection, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Échec de publication de l'état.");
            }
        }
    }

    /// <summary>
    /// Publie l'état audio à chaque changement, et au minimum toutes les 10 s.
    /// Le volume bouge souvent par les touches du clavier : republier tout à chaque
    /// tick saturerait le hub pour rien.
    /// </summary>
    private async Task PublishAudioIfNeededAsync(HubConnection connection, CancellationToken ct)
    {
        var current = audio.Read();
        var stale = DateTimeOffset.UtcNow - _lastAudioPush >= AudioHeartbeat;

        if (!stale && current == _lastAudio)
        {
            return;
        }

        await connection.InvokeAsync(AgentProtocol.ToCore.PushAudioState, current, ct);
        _lastAudio = current;
        _lastAudioPush = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Acquitte une commande audio. Contrairement aux commandes d'extinction, on
    /// acquitte <em>après</em> exécution : le processus survit, et le résultat est
    /// l'information utile.
    /// </summary>
    private async Task AcknowledgeAsync(HubConnection connection, string commandId, bool succeeded)
    {
        var status = succeeded ? "done" : "failed";

        try
        {
            await connection.InvokeAsync(
                AgentProtocol.ToCore.Ack, new CommandAck(commandId, status, null));

            // Pousser l'état sans attendre le prochain tick : l'UI n'a pas de mise à
            // jour optimiste, elle attend le serveur pour se rafraîchir.
            _lastAudio = null;
            await PublishAudioIfNeededAsync(connection, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Impossible d'acquitter {CommandId}.", commandId);
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
