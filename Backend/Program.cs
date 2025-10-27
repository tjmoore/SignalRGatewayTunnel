using Backend.Hubs;
using MessagePack;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console()
    .CreateLogger();

// Backend builds a web app for management use. Optional.
var builder = WebApplication.CreateBuilder(args);

// Turn off resilience in service default as it causes issues with long SignalR connections
// TODO: investigate further
builder.AddServiceDefaults(withResilience:false);

builder.Services.AddSignalR()
    .AddMessagePackProtocol(options =>
    {
        options.SerializerOptions = MessagePackSerializerOptions.Standard
            .WithSecurity(MessagePackSecurity.UntrustedData);
    });
//.AddNewtonsoftJsonProtocol();

// Adding HttpClient also creates a default IHttpMessageHandlerFactory instance which is needed
// for SignalR hub connection with service discovery in TunnelClient
builder.Services.AddHttpClient<RequestForwarder>();
builder.Services.AddHttpClient<TunnelClient>();

builder.Services.AddHostedService<TunnelClient>();

var app = builder.Build();

app.MapDefaultEndpoints();

// Dummy endpoint for backend gateway. Normally the gateway is forwarding to a destination service but
// it might also have its own endpoints for management etc.
app.MapGet("/", () => "This is the gateway backend");

app.Run();
