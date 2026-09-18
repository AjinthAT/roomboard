using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RoomOS.Core.Configuration;
using RoomOS.Core.Data.Entities;

namespace RoomOS.Core.Data;

/// <summary>
/// Applique les migrations et garantit la présence de la pièce et du PC décrits
/// par la configuration. Rejouable sans effet de bord.
/// </summary>
public static class DatabaseSeeder
{
    public const string RoomId = "bedroom";

    public static async Task MigrateAndSeedAsync(IServiceProvider services, CancellationToken ct)
    {
        await using var scope = services.CreateAsyncScope();

        var db = scope.ServiceProvider.GetRequiredService<RoomOsDbContext>();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<RoomOsOptions>>().Value;

        await db.Database.MigrateAsync(ct);

        if (!await db.Rooms.AnyAsync(r => r.Id == RoomId, ct))
        {
            db.Rooms.Add(new Room { Id = RoomId, Name = "Chambre", SortOrder = 0 });
        }

        var pc = await db.Devices.FirstOrDefaultAsync(d => d.Id == options.Pc.Id, ct);
        var config = JsonSerializer.Serialize(
            new PcConfig(options.Pc.Mac, options.Pc.Ip, options.Pc.Broadcast));

        if (pc is null)
        {
            db.Devices.Add(new Device
            {
                Id = options.Pc.Id,
                RoomId = RoomId,
                Kind = DeviceKind.Pc,
                Name = options.Pc.Name,
                ConfigJson = config,
            });
        }
        else
        {
            // La configuration réseau vient de l'environnement : elle fait autorité.
            pc.ConfigJson = config;
            pc.Name = options.Pc.Name;
        }

        await SeedAudioOutputsAsync(db, options, ct);

        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Crée les sorties déclarées en configuration. <c>windows_device_id</c> reste vide :
    /// il est renseigné à la première énumération de l'agent, par correspondance sur
    /// <c>match_hint</c>, puis persisté.
    /// </summary>
    private static async Task SeedAudioOutputsAsync(
        RoomOsDbContext db, RoomOsOptions options, CancellationToken ct)
    {
        var existing = await db.AudioOutputs
            .Where(o => o.PcDeviceId == options.Pc.Id)
            .ToListAsync(ct);

        for (var i = 0; i < options.AudioOutputs.Count; i++)
        {
            var configured = options.AudioOutputs[i];
            var row = existing.FirstOrDefault(o => o.Id == configured.Id);

            if (row is null)
            {
                db.AudioOutputs.Add(new AudioOutput
                {
                    Id = configured.Id,
                    PcDeviceId = options.Pc.Id,
                    MatchHint = configured.MatchHint,
                    FriendlyName = configured.Name,
                    SortOrder = i,
                });
                continue;
            }

            // L'indice et le libellé viennent de l'environnement ; l'identifiant Windows
            // vient de l'agent et ne doit pas être écrasé.
            row.MatchHint = configured.MatchHint;
            row.FriendlyName = configured.Name;
            row.SortOrder = i;
        }
    }
}
