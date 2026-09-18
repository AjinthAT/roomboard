using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using RoomOS.Core.Agents;
using RoomOS.Core.Auth;
using RoomOS.Core.Data;
using RoomOS.Core.Data.Entities;
using RoomOS.Core.Hubs;
using RoomOS.Core.Integrations.Wol;
using RoomOS.Domain.Contracts;

namespace RoomOS.Core.Api;

public static class PcEndpoints
{
    private static readonly TimeSpan WakeTimeout = TimeSpan.FromSeconds(90);
    private static readonly TimeSpan WakePollInterval = TimeSpan.FromSeconds(1);

    public static IEndpointRouteBuilder MapPcEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/pc")
            .RequireAuthorization(TokenAuthenticationHandler.ClientPolicy);

        group.MapPost("/{id}/wake", Wake);
        group.MapPost("/{id}/shutdown", (string id, IHubContext<AgentHub> hub, AgentRegistry registry) =>
            SendToAgent(id, AgentProtocol.ToAgent.Shutdown, hub, registry));
        group.MapPost("/{id}/restart", (string id, IHubContext<AgentHub> hub, AgentRegistry registry) =>
            SendToAgent(id, AgentProtocol.ToAgent.Restart, hub, registry));

        return app;
    }

    /// <summary>
    /// Répond 202 immédiatement et surveille l'arrivée de l'agent en tâche de fond.
    /// Le client apprend le résultat par <c>PcStateChanged</c> sur le hub, jamais par
    /// cette réponse HTTP (docs/05-api.md).
    /// </summary>
    private static async Task<IResult> Wake(
        string id,
        RoomOsDbContext db,
        WakeOnLanSender wol,
        AgentRegistry registry,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var device = await db.Devices.AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id && d.Kind == DeviceKind.Pc, ct);

        if (device is null)
        {
            return Results.NotFound(new { message = $"PC « {id} » inconnu." });
        }

        var config = JsonSerializer.Deserialize<PcConfig>(device.ConfigJson);

        if (config is null || string.IsNullOrWhiteSpace(config.Mac))
        {
            return Results.Problem($"Le PC « {id} » n'a pas d'adresse MAC configurée.");
        }

        if (registry.IsOnline(id))
        {
            // Idempotence : réveiller un PC déjà allumé ne doit rien casser.
            return Results.Accepted(value: new { status = "already-online" });
        }

        await wol.SendAsync(config.Mac, config.Broadcast, ct);

        var logger = loggerFactory.CreateLogger(typeof(PcEndpoints));
        _ = WatchForWakeAsync(id, registry, logger);

        return Results.Accepted(value: new { status = "waking", timeoutSec = (int)WakeTimeout.TotalSeconds });
    }

    /// <summary>
    /// Surveille l'arrivée de l'agent après un réveil. Ne sert qu'à tracer l'échec :
    /// la mise à jour de l'UI vient du hub, pas d'ici.
    /// </summary>
    private static async Task WatchForWakeAsync(string pcId, AgentRegistry registry, ILogger logger)
    {
        var deadline = DateTimeOffset.UtcNow + WakeTimeout;

        while (DateTimeOffset.UtcNow < deadline)
        {
            if (registry.IsOnline(pcId))
            {
                logger.LogInformation("PC {PcId} réveillé.", pcId);
                return;
            }

            await Task.Delay(WakePollInterval);
        }

        logger.LogWarning(
            "PC {PcId} toujours absent après {Timeout} s. WoL activé dans le BIOS et sur la carte réseau ?",
            pcId, WakeTimeout.TotalSeconds);
    }

    private static async Task<IResult> SendToAgent(
        string pcId,
        string method,
        IHubContext<AgentHub> hub,
        AgentRegistry registry)
    {
        var connectionId = registry.GetConnection(pcId);

        if (connectionId is null)
        {
            return Results.Conflict(new { message = $"L'agent de « {pcId} » n'est pas connecté." });
        }

        var commandId = Guid.NewGuid().ToString("n");
        await hub.Clients.Client(connectionId).SendAsync(method, new AgentCommand(commandId));

        return Results.Accepted(value: new { commandId });
    }
}
