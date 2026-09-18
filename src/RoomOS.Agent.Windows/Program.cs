using Microsoft.Extensions.Hosting.WindowsServices;
using RoomOS.Agent.Windows;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService(options => options.ServiceName = "RoomOSAgent");
builder.Services.AddHostedService<AgentWorker>();

var host = builder.Build();
host.Run();
