using Microsoft.EntityFrameworkCore;
using RoomOS.Core.Data.Entities;

namespace RoomOS.Core.Data;

/// <summary>
/// Deux routines de départ, <strong>désactivées</strong>.
/// </summary>
/// <remarks>
/// Désactivées à dessein : une automatisation qui s'allume toute seule le lendemain
/// de l'installation, à une heure qu'on n'a pas choisie, se retourne contre le
/// produit. L'utilisateur règle l'heure puis active.
/// </remarks>
public static class RoutineSeed
{
    private const string Weekdays = "1111100";
    private const string EveryDay = "1111111";

    public static async Task SeedAsync(RoomOsDbContext db, CancellationToken ct)
    {
        var defaults = new (string Id, string Name, string Scene, int Minute, string Days)[]
        {
            ("wake", "Réveil", "work", 7 * 60, Weekdays),
            ("sleep", "Coucher", "night", 23 * 60, EveryDay),
        };

        for (var i = 0; i < defaults.Length; i++)
        {
            var (id, name, scene, minute, days) = defaults[i];

            if (await db.Routines.AnyAsync(r => r.Id == id, ct))
            {
                continue;
            }

            if (!await db.Scenes.AnyAsync(s => s.Id == scene, ct))
            {
                continue;
            }

            db.Routines.Add(new Routine
            {
                Id = id,
                Name = name,
                SceneId = scene,
                MinuteOfDay = minute,
                Days = days,
                Enabled = false,
                SortOrder = i,
            });
        }
    }
}
