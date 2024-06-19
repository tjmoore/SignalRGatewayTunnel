using Backend.Hubs;
using MessagePack;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console()
    .CreateLogger();

// Backend builds a web app for management use. Optional.
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR()
    .AddMessagePackProtocol(options =>
    {
        options.SerializerOptions = MessagePackSerializerOptions.Standard
            .WithSecurity(MessagePackSecurity.UntrustedData);
    });
//.AddNewtonsoftJsonProtocol();

builder.Services
    .AddHostedService<TunnelClient>()
    .AddHttpClient<TunnelClient>();


var app = builder.Build();

app.MapGet("/", () => "This is the gateway backend");

app.Run();
