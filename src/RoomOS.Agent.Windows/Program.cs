using RoomOS.Agent.Windows;
using RoomOS.Agent.Windows.Audio;

// Modes diagnostic : ne se connectent pas au Core.
if (args.Contains("--sensors"))
{
    return SensorDump.Run();
}

if (args.Contains("--audio"))
{
    return AudioDump.Run(args);
}

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<AgentOptions>(builder.Configuration.GetSection(AgentOptions.SectionName));
builder.Services.Configure<TelemetryOptions>(builder.Configuration.GetSection(TelemetryOptions.SectionName));

builder.Services.AddWindowsService(options => options.ServiceName = "RoomOSAgent");
builder.Services.AddSingleton<TelemetryReader>();
builder.Services.AddSingleton<AudioController>();
builder.Services.AddHostedService<AgentWorker>();

var host = builder.Build();
host.Run();

return 0;
