using Microsoft.Extensions.ServiceDiscovery;
using Model;
using Serilog;

namespace Backend.Hubs
{
    public class RequestForwarder
    {
        private readonly HttpClient _httpClient;
        private readonly ServiceEndpointResolver _endPointResolver;

        private readonly string _destinationUrl;

        private string? _resolvedEndpoint = null;

        public RequestForwarder(HttpClient httpClient, ServiceEndpointResolver endPointResolver)
        {
            _httpClient = httpClient;
            _endPointResolver = endPointResolver;
            _destinationUrl = "https+http://destination";

            Log.Debug("RequestForwarder initialized with destination URL {DestinationUrl}", _destinationUrl);
        }

        public async Task<ResponseMessage> ForwardRequest(RequestMessage request, CancellationToken token = default)
        {
            Log.Debug("Received message {@Message}", request);

            using var httpRequest = new HttpRequestMessage();

            string resolvedEndpoint = await GetResolvedEndpoint(token);

            CopyToHttpRequest(request, resolvedEndpoint, httpRequest);

            Log.Debug("Sending to {Url}", httpRequest.RequestUri);

            using var httpResponse = await _httpClient.SendAsync(httpRequest, token);

            var response = await CopyHttpResponse(httpResponse, token);

            Log.Debug("Returning response {@Message}", response);

            return response;
        }

        private static void CopyToHttpRequest(RequestMessage request, string resolvedEndpoint, HttpRequestMessage httpRequest)
        {
            CopyContentAndHeadersToHttpRequest(request, httpRequest);

            var uri = request.RequestUri ?? new Uri("");

            httpRequest.Method = GetMethod(request.Method);
            httpRequest.RequestUri = BuildTargetUri(uri, resolvedEndpoint);
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

        private static Uri BuildTargetUri(Uri requestUri, string resolvedEndpoint)
        {
            return requestUri == null ?
                new Uri(resolvedEndpoint) : new Uri($"{resolvedEndpoint}{requestUri.PathAndQuery}");
        }

        private async Task<string> GetResolvedEndpoint(CancellationToken cancellationToken)
        {
            if (_resolvedEndpoint == null)
            {
                var source = await _endPointResolver.GetEndpointsAsync(_destinationUrl, cancellationToken);
                _resolvedEndpoint = (source.Endpoints.Count > 0) ? source.Endpoints[0].ToString() : null;
                if (string.IsNullOrEmpty(_resolvedEndpoint))
                {
                    throw new ApplicationException("Could not resolve destination service endpoint");
                }

                _resolvedEndpoint = _resolvedEndpoint.TrimEnd('/');
            }
            return _resolvedEndpoint;
        }
    }
}
