using Frontend.Hubs;
using Frontend.Middleware;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console()
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);
builder.Services
    .AddCors()
    .AddSingleton<TunnelHub>();

builder.Services.AddSignalR()
    .AddNewtonsoftJsonProtocol();

var app = builder.Build();

// app.UseHttpsRedirection();
app.UseCors(builder => builder
    .AllowAnyOrigin()
    .AllowAnyMethod()
    .AllowAnyHeader());

app.UseMiddleware<TunnelMiddleware>();

// Hub for connection from backend
app.MapHub<TunnelHub>("/gw-hub");

app.Run();
