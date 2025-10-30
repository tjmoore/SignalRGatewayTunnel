using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.ServiceDiscovery;

namespace Backend.Hubs
{
    public static class HubConnectionExtensions
    {
        /// <summary>
        /// Extension method to set URL with HttpMessageHandlerFactory on hub builder
        /// so that it will pick up Service Discovery
        /// https://github.com/dotnet/aspire/issues/1356#issuecomment-1913232737
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="url"></param>
        /// <param name="messageHandlerFactory"></param>
        /// <returns></returns>
        public static IHubConnectionBuilder WithUrl(
            this IHubConnectionBuilder builder, string url, IHttpMessageHandlerFactory messageHandlerFactory,
            ServiceEndpointResolver endPointResolver)
        {
            return builder.WithUrl(url, options =>
            {
                options.HttpMessageHandlerFactory = _ => messageHandlerFactory.CreateHandler();

                options.WebSocketFactory = async (context, cancellationToken) =>
                {
                    var baseUri = new Uri(await GetResolvedEndpoint(context.Uri.ToString(), endPointResolver, cancellationToken));
                    var wsUri = new UriBuilder(baseUri)
                    {
                        Scheme = baseUri.Scheme == Uri.UriSchemeHttps ? "wss" : "ws",
                        Path = context.Uri.AbsolutePath,
                        Query = context.Uri.Query
                    };

                    var webSocketClient = new System.Net.WebSockets.ClientWebSocket();
                    await webSocketClient.ConnectAsync(wsUri.Uri, cancellationToken);
                    return webSocketClient;
                };
            });
        }

        private static async Task<string> GetResolvedEndpoint(string serviceUrl, ServiceEndpointResolver endpointResolver, CancellationToken cancellationToken)
        {
            var source = await endpointResolver.GetEndpointsAsync(serviceUrl, cancellationToken);
            string? resolvedEndpoint = (source.Endpoints.Count > 0) ? source.Endpoints[0].ToString() : null;
            if (string.IsNullOrEmpty(resolvedEndpoint))
            {
                throw new ApplicationException("Could not resolve service endpoint");
            }

            return resolvedEndpoint;
        }

    }
}
