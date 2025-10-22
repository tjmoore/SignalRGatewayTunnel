using Frontend.Hubs;
using Frontend.Middleware;
using MessagePack;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console()
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services
    .AddCors()
    .AddSingleton<TunnelHub>();

builder.Services.AddSignalR()
    .AddHubOptions<TunnelHub>(options =>
    {
        options.MaximumReceiveMessageSize = 10 * 1024 * 1024; // 10MB
    })
    .AddMessagePackProtocol(options =>
    {
        options.SerializerOptions = MessagePackSerializerOptions.Standard
            .WithSecurity(MessagePackSecurity.UntrustedData);
    });
//    .AddNewtonsoftJsonProtocol();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

// app.UseAntiforgery();

app.MapDefaultEndpoints();

app.UseCors(builder => builder
    .AllowAnyOrigin()
    .AllowAnyMethod()
    .AllowAnyHeader());

app.UseMiddleware<TunnelMiddleware>();

// Hub for connection from backend
app.MapHub<TunnelHub>("/gw-hub");

app.Run();
