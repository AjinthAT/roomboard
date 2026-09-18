using RoomOS.Agent.Windows;

// Mode diagnostic : liste les capteurs et sort, sans se connecter au Core.
if (args.Contains("--sensors"))
{
    return SensorDump.Run();
}

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<AgentOptions>(builder.Configuration.GetSection(AgentOptions.SectionName));
builder.Services.Configure<TelemetryOptions>(builder.Configuration.GetSection(TelemetryOptions.SectionName));

builder.Services.AddWindowsService(options => options.ServiceName = "RoomOSAgent");
builder.Services.AddSingleton<TelemetryReader>();
builder.Services.AddHostedService<AgentWorker>();

var host = builder.Build();
host.Run();

return 0;
