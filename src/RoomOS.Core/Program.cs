using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using RoomOS.Core.Agents;
using RoomOS.Core.Api;
using RoomOS.Core.Audio;
using RoomOS.Core.Auth;
using RoomOS.Core.Configuration;
using RoomOS.Core.Data;
using RoomOS.Core.Hubs;
using RoomOS.Core.Integrations.Mqtt;
using RoomOS.Core.Observability;
using RoomOS.Core.Integrations.Spotify;
using RoomOS.Core.Integrations.Wol;
using RoomOS.Core.Scenes;
using RoomOS.Core.Scenes.Executors;
using RoomOS.Core.State;
using RoomOS.Domain.Contracts;

var builder = WebApplication.CreateBuilder(args);

// camelCase une fois pour toutes (docs/11-conventions.md)
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
});

builder.Services.Configure<RoomOsOptions>(builder.Configuration.GetSection(RoomOsOptions.SectionName));

var options = builder.Configuration.GetSection(RoomOsOptions.SectionName).Get<RoomOsOptions>() ?? new RoomOsOptions();

builder.Services.AddDbContext<RoomOsDbContext>(db =>
    db.UseSqlite($"Data Source={options.DatabasePath}"));

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<StateStore>();
builder.Services.AddSingleton<AgentRegistry>();
builder.Services.AddSingleton<WakeOnLanSender>();
builder.Services.AddScoped<AudioOutputResolver>();

builder.Services.AddHttpClient(SpotifyMusicProvider.HttpClientName);
builder.Services.AddSingleton<SpotifyAuthService>();
builder.Services.AddScoped<IMusicProvider, SpotifyMusicProvider>();
builder.Services.AddHostedService<MusicPoller>();

// Enregistré comme singleton puis exposé en service hébergé : les endpoints ont
// besoin de la même instance pour publier des commandes.
builder.Services.AddSingleton<MqttLightService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<MqttLightService>());

builder.Services.AddSingleton<SceneEngine>();
builder.Services.AddSingleton<IStepExecutor, PcWakeExecutor>();
builder.Services.AddSingleton<IStepExecutor, PcShutdownExecutor>();
builder.Services.AddSingleton<IStepExecutor, AudioSetOutputExecutor>();
builder.Services.AddSingleton<IStepExecutor, AudioSetVolumeExecutor>();
builder.Services.AddSingleton<IStepExecutor, AudioSetMuteExecutor>();
builder.Services.AddSingleton<IStepExecutor, LightSetExecutor>();
builder.Services.AddSingleton<IStepExecutor, MusicTransferExecutor>();
builder.Services.AddSingleton<IStepExecutor, MusicPlayExecutor>();
builder.Services.AddSingleton<IStepExecutor, MusicPauseExecutor>();
builder.Services.AddSingleton<IStepExecutor, MusicSetVolumeExecutor>();
builder.Services.AddSingleton<IStepExecutor, DelayExecutor>();
builder.Services.AddHostedService<RoomBroadcaster>();

builder.Services.AddSignalR();

builder.Services
    .AddAuthentication(TokenAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, TokenAuthenticationHandler>(
        TokenAuthenticationHandler.SchemeName, null);

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(TokenAuthenticationHandler.ClientPolicy, policy =>
        policy.RequireRole(TokenAuthenticationHandler.ClientRole))
    .AddPolicy(TokenAuthenticationHandler.AgentPolicy, policy =>
        policy.RequireRole(TokenAuthenticationHandler.AgentRole));

var app = builder.Build();

if (string.IsNullOrWhiteSpace(options.ApiToken) || string.IsNullOrWhiteSpace(options.AgentToken))
{
    // Échouer au démarrage plutôt que servir une API ouverte : un jeton vide
    // n'authentifie personne, et le handler refuserait tout le monde en silence.
    throw new InvalidOperationException(
        "ROOMOS__ApiToken et ROOMOS__AgentToken doivent être définis. Voir deploy/.env.example.");
}

await DatabaseSeeder.MigrateAndSeedAsync(app.Services, CancellationToken.None);

// Le Core sert le front : une seule origine, donc pas de CORS.
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0";

// Sans authentification : sert à savoir si le Core est vivant, y compris depuis
// un front qui n'a pas encore de jeton.
app.MapGet("/healthz", () => new HealthResponse("ok", version, DateTimeOffset.UtcNow));

app.MapStateEndpoints();
app.MapPcEndpoints();
app.MapAudioEndpoints();
app.MapMusicEndpoints();
app.MapLightEndpoints();
app.MapSceneEndpoints();
app.MapMetricsEndpoint();

app.MapHub<RoomHub>("/hub/room");
app.MapHub<AgentHub>("/hub/agent");

// Les routes inconnues sous /api et /hub répondent 404. Sans ça, le fallback SPA
// ci-dessous leur sert index.html en 200, et une faute de frappe côté client devient
// une erreur de parsing JSON au lieu d'un statut clair. Ces motifs attrape-tout ont
// une précédence plus faible que les routes déclarées : ils ne les masquent pas.
app.Map("/api/{**rest}", () => Results.NotFound());
app.Map("/hub/{**rest}", () => Results.NotFound());

// Toute autre route inconnue rend la PWA : une seule page, pas de router serveur.
app.MapFallbackToFile("index.html");

app.Run();

/// <summary>Exposé pour <c>WebApplicationFactory</c> dans les tests.</summary>
public partial class Program;
