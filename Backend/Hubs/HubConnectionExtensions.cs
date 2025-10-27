using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;

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
        /// <param name="clientFactory"></param>
        /// <returns></returns>
        public static IHubConnectionBuilder WithUrl(this IHubConnectionBuilder builder, string url, IHttpMessageHandlerFactory clientFactory)
        {
            return builder.WithUrl(url, options =>
            {
                options.HttpMessageHandlerFactory = _ => clientFactory.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;

                // WebSockets transport with Service Discovery is not yet supported
                // options.Transports = HttpTransportType.WebSockets | HttpTransportType.LongPolling;
            });
        }
    }
}
