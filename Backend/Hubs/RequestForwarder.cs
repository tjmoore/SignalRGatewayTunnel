using Model;
using Serilog;

namespace Backend.Hubs
{
    public class RequestForwarder
    {
        private readonly HttpClient _httpClient;

        public RequestForwarder(HttpClient httpClient)
        {
            _httpClient = httpClient;

            // Destination URL is configured in HttpClient base address
            if (httpClient.BaseAddress == null)
            {
                throw new ArgumentException("HttpClient must have a BaseAddress configured for the destination URL.");
            }
            Log.Debug("RequestForwarder initialized with destination URL {DestinationUrl}", httpClient.BaseAddress);
        }

        public async Task<ResponseMessage> ForwardRequest(RequestMessage request, CancellationToken token = default)
        {
            Log.Debug("Received message {@Message}", request);

            using var httpRequest = new HttpRequestMessage();

            CopyToHttpRequest(request, httpRequest);

            Log.Debug("Sending to {Url}", httpRequest.RequestUri);

            using var httpResponse = await _httpClient.SendAsync(httpRequest, token);

            var response = await CopyHttpResponse(httpResponse, token);

            Log.Debug("Returning response {@Message}", response);

            return response;
        }

        private static void CopyToHttpRequest(RequestMessage request, HttpRequestMessage httpRequest)
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

        private static async Task<ResponseMessage> CopyHttpResponse(HttpResponseMessage httpResponse, CancellationToken token)
        {
            var response = new ResponseMessage
            {
                StatusCode = httpResponse.StatusCode,
            };

            if (httpResponse.Content != null)
            {
                using var ms = new MemoryStream();
                await httpResponse.Content.CopyToAsync(ms, token);
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

        private static Uri BuildTargetUri(RequestMessage request)
        {
            // Set the relative path for target as the destination base is set in the HttpClient
            return request.RequestUri == null ?
                new Uri("/", UriKind.Relative) : new Uri(request.RequestUri.PathAndQuery, UriKind.Relative);
        }
    }
}
