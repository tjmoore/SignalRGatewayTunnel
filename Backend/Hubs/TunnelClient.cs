using MessagePack;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.ServiceDiscovery;
using Model;
using Serilog;
using System.Threading;

namespace Backend.Hubs
{
    /// <summary>
    /// SignalR client for tunnel connections.
    /// This connects to hub on the backend and listens for messages.
    /// </summary>
    public class TunnelClient : IHostedService
    {
        private readonly HubConnection _connection;
        private readonly RequestForwarder _requestForwarder;

        private readonly string _clientId = "my_backend_client";

        public TunnelClient(IHttpMessageHandlerFactory httpClientFactory, RequestForwarder requestForwarder)
        {
            _requestForwarder = requestForwarder;

            // TODO: wss+ws support with service discovery. Currently only http+https is supported.
            // string tunnelUrl = "wss+ws://frontend/gw-hub";
            string tunnelUrl = "https+http://frontend/gw-hub";

            Log.Debug("TunnelClient initialized with tunnel URL {TunnelUrl}", tunnelUrl);

            // Using extension method to set HttpMessageHandlerFactory on hub builder so that it
            // can use Service Discovery as it doesn't support it by default

            _connection = new HubConnectionBuilder()
                .WithUrl(tunnelUrl, httpClientFactory)
                .WithAutomaticReconnect()
                .AddMessagePackProtocol(options =>
                {
                    options.SerializerOptions = MessagePackSerializerOptions.Standard
                        .WithSecurity(MessagePackSecurity.UntrustedData);
                })
                //.AddNewtonsoftJsonProtocol()
                .ConfigureLogging(logging =>
                {
                    logging.AddConsole();
                    logging.SetMinimumLevel(LogLevel.Debug);
                })
                .Build();

            _connection.Closed += error =>
            {
                if (_connection.State == HubConnectionState.Disconnected)
                    Log.Information("Hub disconnected");

                return Task.CompletedTask;
            };

            _connection.Reconnecting += error =>
            {
                if (_connection.State == HubConnectionState.Reconnecting)
                    Log.Information("Hub reconnecting");

                return Task.CompletedTask;
            };

            _connection.Reconnected += async connectionId =>
            {
                if (_connection.State == HubConnectionState.Connected)
                {
                    Log.Information("Hub connected");
                    await OnConnected(default);
                }
            };

            _connection.On("HttpRequest", async (RequestMessage request) =>
            {
                // TODO: can a cancellation token be passed here?

                return await _requestForwarder.ForwardRequest(request);
            });
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await ConnectWithRetryAsync(_connection, cancellationToken);

            await OnConnected(cancellationToken);
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await _connection.StopAsync(cancellationToken);
        }

        private async Task OnConnected(CancellationToken cancellationToken)
        {
            if (await _connection.InvokeAsync<bool>("Register", _clientId, cancellationToken))
                Log.Information("Successfully registered with hub");
            else
                Log.Information("Failed to register with hub");
        }

        // Retry logic based on examples at https://learn.microsoft.com/en-us/aspnet/core/signalr/dotnet-client?view=aspnetcore-8.0&tabs=visual-studio

        private static async Task<bool> ConnectWithRetryAsync(HubConnection connection, CancellationToken token)
        {
            // Keep trying to until we can start or the token is canceled.
            while (true)
            {
                try
                {
                    await connection.StartAsync(token);
                    if (connection.State == HubConnectionState.Connected)
                    {
                        Log.Information("Hub connected");
                        return true;
                    }

                    throw new FailedConnectionException($"State: {connection.State}");
                }
                catch when (token.IsCancellationRequested)
                {
                    Log.Information("Hub connection stopping due to cancellation");
                    return false;
                }
                catch (Exception ex)
                {
                    Log.Information(ex, "Failed to connect: {Message}", ex);

                    // Failed to connect, trying again in 5000 ms.
                    if (connection.State == HubConnectionState.Disconnected)
                        Log.Information("Hub disconnected");

                    await Task.Delay(5000, token);
                }
            }
        }

        private class FailedConnectionException(string message) : Exception(message)
        {}
    }
}
