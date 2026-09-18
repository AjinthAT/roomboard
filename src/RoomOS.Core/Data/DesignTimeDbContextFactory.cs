using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace RoomOS.Core.Data;

/// <summary>
/// Utilisée uniquement par <c>dotnet ef</c>. Évite d'exécuter <c>Program.cs</c>,
/// qui exige des jetons configurés pour démarrer.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<RoomOsDbContext>
{
    public RoomOsDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<RoomOsDbContext>()
            .UseSqlite("Data Source=design-time.db")
            .Options;

        return new RoomOsDbContext(options);
    }
}
