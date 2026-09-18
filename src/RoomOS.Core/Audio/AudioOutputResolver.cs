using Microsoft.EntityFrameworkCore;
using RoomOS.Core.Data;
using RoomOS.Core.Data.Entities;
using RoomOS.Domain.Contracts;

namespace RoomOS.Core.Audio;

/// <summary>
/// Fait le lien entre les périphériques énumérés par l'agent et les sorties
/// enregistrées en base.
/// </summary>
/// <remarks>
/// L'identifiant Windows est la clé. Quand il est inconnu — première exécution,
/// réinstallation de pilote, changement de port USB — on retombe sur l'indice de
/// nom, puis on persiste le nouvel identifiant pour ne pas refaire la recherche
/// approximative à chaque fois (docs/04-domaine.md).
/// </remarks>
public sealed class AudioOutputResolver(RoomOsDbContext db, ILogger<AudioOutputResolver> logger)
{
    public async Task<AudioState> ResolveAsync(
        string pcId, AgentAudioState reported, DateTimeOffset now, CancellationToken ct)
    {
        var rows = await db.AudioOutputs
            .Where(o => o.PcDeviceId == pcId)
            .OrderBy(o => o.SortOrder)
            .ToListAsync(ct);

        var changed = false;

        foreach (var row in rows)
        {
            var match = FindDevice(row, reported.Outputs);

            if (match is not null && row.WindowsDeviceId != match.WindowsDeviceId)
            {
                logger.LogInformation(
                    "Sortie {OutputId} associée à « {Name} » ({WindowsDeviceId}).",
                    row.Id, match.Name, match.WindowsDeviceId);

                row.WindowsDeviceId = match.WindowsDeviceId;
                changed = true;
            }
        }

        if (changed)
        {
            await db.SaveChangesAsync(ct);
        }

        var outputs = rows
            .Select(row => new AudioOutputInfo(
                row.Id,
                row.FriendlyName,
                reported.Outputs.Any(d => d.WindowsDeviceId == row.WindowsDeviceId)))
            .ToList();

        var active = rows.FirstOrDefault(
            row => row.WindowsDeviceId is not null &&
                   row.WindowsDeviceId == reported.ActiveWindowsDeviceId);

        if (active is null && reported.ActiveWindowsDeviceId is not null)
        {
            // Cas nominal : sortie active hors du registre (HDMI, Bluetooth de passage).
            logger.LogDebug(
                "Sortie active {WindowsDeviceId} non enregistrée.", reported.ActiveWindowsDeviceId);
        }

        return new AudioState(active?.Id, reported.Volume, reported.Muted, outputs, now);
    }

    /// <summary>Identifiant Windows d'abord, indice de nom ensuite.</summary>
    private static WindowsAudioOutput? FindDevice(
        AudioOutput row, IReadOnlyList<WindowsAudioOutput> devices)
    {
        var byId = devices.FirstOrDefault(d => d.WindowsDeviceId == row.WindowsDeviceId);

        if (byId is not null)
        {
            return byId;
        }

        return string.IsNullOrWhiteSpace(row.MatchHint)
            ? null
            : devices.FirstOrDefault(
                d => d.Name.Contains(row.MatchHint, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Identifiant Windows d'une sortie, pour envoyer une commande à l'agent.</summary>
    public Task<string?> GetWindowsDeviceIdAsync(string pcId, string outputId, CancellationToken ct) =>
        db.AudioOutputs
            .Where(o => o.PcDeviceId == pcId && o.Id == outputId)
            .Select(o => o.WindowsDeviceId)
            .FirstOrDefaultAsync(ct);
}
