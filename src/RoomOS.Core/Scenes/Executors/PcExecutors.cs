using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using RoomOS.Core.Agents;
using RoomOS.Core.Data;
using RoomOS.Core.Data.Entities;
using RoomOS.Core.Hubs;
using RoomOS.Core.Integrations.Wol;
using RoomOS.Domain.Contracts;

namespace RoomOS.Core.Scenes.Executors;

/// <summary>
/// Réveille le PC. Avec <c>waitForOnline</c>, attend l'agent : le moteur applique le
/// timeout de l'étape, il suffit donc de sonder jusqu'à l'annulation.
/// </summary>
public sealed class PcWakeExecutor(
    IServiceScopeFactory scopeFactory,
    AgentRegistry registry,
    WakeOnLanSender wol) : IStepExecutor
{
    public string Type => SceneStepTypes.PcWake;

    public async Task ExecuteAsync(SceneStep step, CancellationToken ct)
    {
        var pcId = step.DeviceId ?? throw new InvalidOperationException("deviceId manquant.");

        if (registry.IsOnline(pcId))
        {
            // Idempotence : relancer une scène sur un PC déjà allumé ne fait rien.
            return;
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RoomOsDbContext>();

        var device = await db.Devices.AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == pcId && d.Kind == DeviceKind.Pc, ct)
            ?? throw new InvalidOperationException($"PC « {pcId} » inconnu.");

        var config = JsonSerializer.Deserialize<PcConfig>(device.ConfigJson)
            ?? throw new InvalidOperationException($"Configuration illisible pour « {pcId} ».");

        await wol.SendAsync(config.Mac, config.Broadcast, ct);

        if (!step.WaitForOnline)
        {
            return;
        }

        while (!registry.IsOnline(pcId))
        {
            // Annulé par le timeout de l'étape : l'exception remonte au moteur, qui
            // marque l'étape en échec et interrompt la scène puisque pc.wake est
            // bloquante (docs/08-scenes.md, règle 3).
            await Task.Delay(TimeSpan.FromSeconds(1), ct);
        }
    }
}

public sealed class PcShutdownExecutor(
    AgentRegistry registry, IHubContext<AgentHub> hub) : IStepExecutor
{
    public string Type => SceneStepTypes.PcShutdown;

    public async Task ExecuteAsync(SceneStep step, CancellationToken ct)
    {
        var pcId = step.DeviceId ?? throw new InvalidOperationException("deviceId manquant.");
        var connectionId = registry.GetConnection(pcId);

        if (connectionId is null)
        {
            // Déjà éteint : la scène Night ne doit pas échouer parce que le PC l'était
            // déjà.
            return;
        }

        await hub.Clients.Client(connectionId).SendAsync(
            AgentProtocol.ToAgent.Shutdown,
            new AgentCommand(Guid.NewGuid().ToString("n")),
            ct);
    }
}
