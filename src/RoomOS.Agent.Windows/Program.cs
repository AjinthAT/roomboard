using RoomOS.Agent.Windows;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<AgentOptions>(builder.Configuration.GetSection(AgentOptions.SectionName));
builder.Services.Configure<TelemetryOptions>(builder.Configuration.GetSection(TelemetryOptions.SectionName));

builder.Services.AddWindowsService(options => options.ServiceName = "RoomOSAgent");
builder.Services.AddSingleton<TelemetryReader>();
builder.Services.AddHostedService<AgentWorker>();

var host = builder.Build();
host.Run();
