using System.Reflection;
using System.Text.Json;
using RoomOS.Domain.Contracts;

var builder = WebApplication.CreateBuilder(args);

// camelCase une fois pour toutes (docs/11-conventions.md)
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
});

var app = builder.Build();

// Le Core sert le front : une seule origine, donc pas de CORS.
app.UseDefaultFiles();
app.UseStaticFiles();

var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0";

app.MapGet("/healthz", () => new HealthResponse("ok", version, DateTimeOffset.UtcNow));

// Toute route inconnue rend la PWA : une seule page, pas de router serveur.
app.MapFallbackToFile("index.html");

app.Run();

/// <summary>Exposé pour <c>WebApplicationFactory</c> dans les tests.</summary>
public partial class Program;
