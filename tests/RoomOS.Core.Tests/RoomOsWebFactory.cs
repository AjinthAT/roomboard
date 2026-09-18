using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace RoomOS.Core.Tests;

/// <summary>
/// Hôte de test. Fournit les jetons — sans eux le Core refuse de démarrer, ce qui
/// est voulu — et une base SQLite jetable par instance.
/// </summary>
public sealed class RoomOsWebFactory : WebApplicationFactory<Program>, IDisposable
{
    public const string ApiToken = "test-api-token";
    public const string AgentToken = "test-agent-token";

    private readonly string _databasePath =
        Path.Combine(Path.GetTempPath(), $"roomos-tests-{Guid.NewGuid():n}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ROOMOS:ApiToken", ApiToken);
        builder.UseSetting("ROOMOS:AgentToken", AgentToken);
        builder.UseSetting("ROOMOS:DatabasePath", _databasePath);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing && File.Exists(_databasePath))
        {
            File.Delete(_databasePath);
        }
    }
}
