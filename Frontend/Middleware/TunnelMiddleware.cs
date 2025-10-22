using Frontend.Hubs;
using Microsoft.AspNetCore.Http.Extensions;
using Model;
using Serilog;

namespace Frontend.Middleware
{
    public class TunnelMiddleware(RequestDelegate nextMiddleware, TunnelHub tunnelHub)
    {
        // Loosely based on https://auth0.com/blog/building-a-reverse-proxy-in-dot-net-core/ using SignalR to tunnel instead of forwarding via HttpClient

        private readonly RequestDelegate _nextMiddleware = nextMiddleware;
        private readonly TunnelHub _tunnelHub = tunnelHub;

        public async Task Invoke(HttpContext context)
        {
            var path = context.Request.Path;

            if (!path.StartsWithSegments("/gw-hub") &&
                !path.StartsWithSegments("/health") &&
                !path.StartsWithSegments("/alive"))
            {
                var tunnelRequestMessage = await CreateTunnelMessage(context);

                Log.Debug("Sending request {@Message}", tunnelRequestMessage);

                var responseMessage = await _tunnelHub.SendHttpRequestAsync(tunnelRequestMessage);
                if (responseMessage == null)
                {
                    context.Response.StatusCode = StatusCodes.Status502BadGateway;
                    return;
                }

                context.Response.StatusCode = (int)responseMessage.StatusCode;
                CopyFromResponseHeaders(context, responseMessage);
                if (responseMessage.Content != null)
                {
                    using var ms = new MemoryStream(responseMessage.Content);
                    await ms.CopyToAsync(context.Response.Body);
                }

                Log.Debug("Received response {@Message}", responseMessage);

                return;
            }

            await _nextMiddleware(context);
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

        private static async Task<RequestMessage> CreateTunnelMessage(HttpContext context)
        {
            var requestMessage = new RequestMessage();
            await CopyFromOriginalRequestContentAndHeaders(context, requestMessage);

            requestMessage.RequestUri = BuildTunnelUri(context.Request);
            requestMessage.Method = context.Request.Method;

            return requestMessage;
        }

        private static async Task CopyFromOriginalRequestContentAndHeaders(HttpContext context, RequestMessage requestMessage)
        {
            var requestMethod = context.Request.Method;

            if (!HttpMethods.IsGet(requestMethod) &&
              !HttpMethods.IsHead(requestMethod) &&
              !HttpMethods.IsDelete(requestMethod) &&
              !HttpMethods.IsTrace(requestMethod))
            {
                using var ms = new MemoryStream();
                await context.Request.Body.CopyToAsync(ms);
                requestMessage.Content = ms.ToArray();

                foreach (var header in context.Request.Headers.Where(kvp => IsContentHeader(kvp.Key)))
                {
                    requestMessage.ContentHeaders.Add(new KeyValuePair<string, IEnumerable<string?>>(header.Key, [.. header.Value]));
                }
            }

            foreach (var header in context.Request.Headers.Where(kvp => !IsContentHeader(kvp.Key)))
            {
                requestMessage.Headers.Add(new KeyValuePair<string, IEnumerable<string?>>(header.Key, [.. header.Value]));
            }
        }

        private static void CopyFromResponseHeaders(HttpContext context, ResponseMessage responseMessage)
        {
            foreach (var header in responseMessage.Headers)
            {
                context.Response.Headers[header.Key] = header.Value.ToArray();
            }

            foreach (var header in responseMessage.ContentHeaders)
            {
                context.Response.Headers[header.Key] = header.Value.ToArray();
            }

            context.Response.Headers.Remove("transfer-encoding");
        }

        private static Uri BuildTunnelUri(HttpRequest request)
        {
            // Change URL to a dummy host. The client will retarget to the destination as appropriate.
            var uriBuilder = new UriBuilder(request.GetDisplayUrl())
            {
                Scheme = "http",
                Host = "tunnel",
                Port = 80
            };

            return uriBuilder.Uri;
        }

        private static bool IsContentHeader(string header)
        {
            return header.StartsWith("Content-") ||
                header.Equals("Expires", StringComparison.OrdinalIgnoreCase) ||
                header.Equals("Last-Modified", StringComparison.OrdinalIgnoreCase);
        }
    }
}
