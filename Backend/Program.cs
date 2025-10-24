using Backend.Hubs;
using MessagePack;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console()
    .CreateLogger();

// Backend builds a web app for management use. Optional.
var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults(withResilience:false);

builder.Services.AddSignalR()
    .AddMessagePackProtocol(options =>
    {
        options.SerializerOptions = MessagePackSerializerOptions.Standard
            .WithSecurity(MessagePackSecurity.UntrustedData);
    });
//.AddNewtonsoftJsonProtocol();

builder.Services.AddHttpClient<RequestForwarder>(static client =>
    {
        // Service Discovery URL for the destination service
        client.BaseAddress = new("https+http://destination");
    });

builder.Services.AddHostedService(sp =>
    // Factory construct TunnelClient passing in the Service Discovery URL as
    // SignalR hub doesn't support setting HttpClient
    new TunnelClient(
        sp.GetRequiredService<IHttpMessageHandlerFactory>(),
        sp.GetRequiredService<RequestForwarder>(),
        "https+http://frontend/gw-hub")); // Defaults to long polling transport
        // "wss+ws://frontend/gw-hub")); // Name resolution doesn't work with this, but is needed for websockets

var app = builder.Build();

app.MapDefaultEndpoints();

// Dummy endpoint for backend gateway. Normally the gatewat us forwarding to a destination service but
// it might also have its own endpoints for management etc.
app.MapGet("/", () => "This is the gateway backend");

app.Run();
