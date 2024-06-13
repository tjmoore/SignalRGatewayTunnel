using Microsoft.AspNetCore.SignalR.Client;
using Serilog;

namespace Backend.Hubs
{
    /// <summary>
    /// SignalR client for tunnel connections.
    /// This connects to hub on the backend and listens for messages.
    /// </summary>
    public class TunnelClient : IHostedService
    {
        private readonly HubConnection _connection;

        public TunnelClient()
        {
            _connection = new HubConnectionBuilder()
                .WithUrl("http://localhost:5105/gw-hub")
                .WithAutomaticReconnect()
                .AddNewtonsoftJsonProtocol()
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

            _connection.On<bool>("IsRegistered", (isRegistered) =>
            {
                Log.Information("Successfully registered with hub");
            });

            _connection.On("HttpRequest", (HttpRequestMessage request) =>
            {
                Log.Debug("Received message {@Message}", request);
                return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
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
            // TODO: send unique client identifier and other info if necessary

            if (await _connection.InvokeAsync<bool>("Register", cancellationToken))
                Log.Information("Successfully registered with hub");
            else
                Log.Information("Failed to register with hub");
        }

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
        {
        }
    }
}
