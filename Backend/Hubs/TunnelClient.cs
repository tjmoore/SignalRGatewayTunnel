using MessagePack;
using Microsoft.AspNetCore.SignalR.Client;
using Model;
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
        private readonly HttpClient _httpClient;

        private readonly Uri _destinationUrl;

        private readonly string _clientId = "my_backend_client";

        public TunnelClient(HttpClient httpClient, IConfiguration config)
        {
            _httpClient = httpClient;

            string? tunnelUrl = config["TunnelEndpoint:Url"];
            if (string.IsNullOrEmpty(tunnelUrl))
                throw new MissingConfigException("Missing config TunnelEndpoint:Url");

            string? destinationUrl = config["DestinationEndpoint:Url"];
            if (string.IsNullOrEmpty(destinationUrl))
                throw new MissingConfigException("Missing config DestinationEndpoint:Url");

            _destinationUrl = new Uri(destinationUrl);

            _connection = new HubConnectionBuilder()
                .WithUrl(tunnelUrl)
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
                Log.Debug("Received message {@Message}", request);

                using var httpRequest = new HttpRequestMessage();

                CopyToHttpRequest(request, httpRequest);

                Log.Debug("Sending to {Url}", httpRequest.RequestUri);

                using var httpResponse = await _httpClient.SendAsync(httpRequest);

                var response = await CopyHttpResponse(httpResponse);

                Log.Debug("Returning response {@Message}", response);

                return response;
            });
        }

        private static string GetContentString(byte[]? content, int maxLength = 80)
        {
            if (content == null)
                return "";

            string str = System.Text.Encoding.UTF8.GetString(content);

            if (str.Length > maxLength)
                return str[..maxLength];

            return str;
        }

        private void CopyToHttpRequest(RequestMessage request, HttpRequestMessage httpRequest)
        {
            CopyContentAndHeadersToHttpRequest(request, httpRequest);

            httpRequest.Method = GetMethod(request.Method);
            httpRequest.RequestUri = BuildTargetUri(request);
            httpRequest.Headers.Host = httpRequest.RequestUri.Host;
        }

        private static void CopyContentAndHeadersToHttpRequest(RequestMessage request, HttpRequestMessage httpRequest)
        {
            var requestMethod = request.Method;

            if (!HttpMethods.IsGet(requestMethod) &&
              !HttpMethods.IsHead(requestMethod) &&
              !HttpMethods.IsDelete(requestMethod) &&
              !HttpMethods.IsTrace(requestMethod) &&
              request.Content != null)
            {
                var ms = new MemoryStream(request.Content); // Don't dispose here
                var streamContent = new StreamContent(ms);
                httpRequest.Content = streamContent;
            }

            foreach (var header in request.Headers)
            {
                httpRequest.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
            }

            foreach (var header in request.ContentHeaders)
            {
                httpRequest.Content?.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
            }
        }

        private static async Task<ResponseMessage> CopyHttpResponse(HttpResponseMessage httpResponse)
        {
            var response = new ResponseMessage
            {
                StatusCode = httpResponse.StatusCode,
            };

            if (httpResponse.Content != null)
            {
                using var ms = new MemoryStream();
                await httpResponse.Content.CopyToAsync(ms);
                response.Content = ms.ToArray();
            }

            CopyFromHttpResponseHeaders(response, httpResponse);

            return response;
        }

        private static void CopyFromHttpResponseHeaders(ResponseMessage response, HttpResponseMessage httpResponse)
        {
            foreach (var header in httpResponse.Headers)
            {
                response.Headers.Add(new KeyValuePair<string, IEnumerable<string?>>(header.Key, [.. header.Value]));
            }

            foreach (var header in httpResponse.Content.Headers)
            {
                response.ContentHeaders.Add(new KeyValuePair<string, IEnumerable<string?>>(header.Key, [.. header.Value]));
            }
        }


        private static HttpMethod GetMethod(string method)
        {
            if (HttpMethods.IsDelete(method)) return HttpMethod.Delete;
            if (HttpMethods.IsGet(method)) return HttpMethod.Get;
            if (HttpMethods.IsHead(method)) return HttpMethod.Head;
            if (HttpMethods.IsOptions(method)) return HttpMethod.Options;
            if (HttpMethods.IsPost(method)) return HttpMethod.Post;
            if (HttpMethods.IsPut(method)) return HttpMethod.Put;
            if (HttpMethods.IsTrace(method)) return HttpMethod.Trace;
            return new HttpMethod(method);
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

        private Uri BuildTargetUri(RequestMessage request)
        {
            // Retarget to local destination
            var uriBuilder = request.RequestUri != null ? new UriBuilder(request.RequestUri) : new UriBuilder();

            uriBuilder.Scheme = _destinationUrl.Scheme;
            uriBuilder.Host = _destinationUrl.Host;
            uriBuilder.Port = _destinationUrl.Port;

            return uriBuilder.Uri;
        }

        private class FailedConnectionException(string message) : Exception(message)
        {}

        private class MissingConfigException(string message) : Exception(message)
        {}
    }
}
