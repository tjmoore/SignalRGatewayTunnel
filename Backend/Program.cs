using Backend.Hubs;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console()
    .CreateLogger();

// Backend builds a web app for management use. Optional.
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR();

builder.Services.AddHostedService<TunnelClient>();


var app = builder.Build();

app.MapGet("/", () => "This is the gateway backend");

app.Run();
