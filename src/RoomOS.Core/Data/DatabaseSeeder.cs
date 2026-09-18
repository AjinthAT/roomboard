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

    /// <summary>
    /// Utilisées quand <c>ROOMOS__AudioOutputs__*</c> n'est pas renseigné. Les indices
    /// visent le matériel, pas le rôle Windows : « Casque » correspondrait à
    /// « Casque pour téléphone », le canal communications en bande étroite.
    /// </summary>
    private static readonly List<AudioOutputOptions> DefaultAudioOutputs =
    [
        new() { Id = "jbl", Name = "JBL", MatchHint = "JBL" },
        new() { Id = "headset", Name = "Casque", MatchHint = "CORSAIR" },
    ];

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
        await SeedLightsAsync(db, options, ct);

        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Crée les lampes déclarées en configuration. Aucune par défaut : tant qu'aucune
    /// ampoule n'est appairée, il n'y a rien à afficher, et une lampe fantôme serait
    /// pire qu'une carte vide.
    /// </summary>
    private static async Task SeedLightsAsync(
        RoomOsDbContext db, RoomOsOptions options, CancellationToken ct)
    {
        foreach (var configured in options.Lights)
        {
            if (string.IsNullOrWhiteSpace(configured.Id))
            {
                continue;
            }

            var config = JsonSerializer.Serialize(new LightConfig(
                configured.Z2mFriendlyName, configured.SupportsColor, configured.SupportsBrightness));

            var existing = await db.Devices.FirstOrDefaultAsync(d => d.Id == configured.Id, ct);

            if (existing is null)
            {
                db.Devices.Add(new Device
                {
                    Id = configured.Id,
                    RoomId = RoomId,
                    Kind = DeviceKind.Light,
                    Name = configured.Name,
                    ConfigJson = config,
                });
            }
            else
            {
                existing.Name = configured.Name;
                existing.ConfigJson = config;
            }
        }
    }

    /// <summary>
    /// Crée les sorties déclarées en configuration. <c>windows_device_id</c> reste vide :
    /// il est renseigné à la première énumération de l'agent, par correspondance sur
    /// <c>match_hint</c>, puis persisté.
    /// </summary>
    private static async Task SeedAudioOutputsAsync(
        RoomOsDbContext db, RoomOsOptions options, CancellationToken ct)
    {
        var configuredOutputs = options.AudioOutputs.Count > 0
            ? options.AudioOutputs
            : DefaultAudioOutputs;

        var existing = await db.AudioOutputs
            .Where(o => o.PcDeviceId == options.Pc.Id)
            .ToListAsync(ct);

        for (var i = 0; i < configuredOutputs.Count; i++)
        {
            var configured = configuredOutputs[i];
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
