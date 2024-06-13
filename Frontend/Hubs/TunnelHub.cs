using Microsoft.AspNetCore.SignalR;
using Serilog;
using System.Collections.Concurrent;

namespace Frontend.Hubs
{
    public interface ITunnel
    {
        Task<HttpResponseMessage> HttpRequest(HttpRequestMessage request);
    }

    public class TunnelHub : Hub<ITunnel>
    {
        // TODO: use a client identifier and User or find connection from that identifier. Connection IDs change and may not scale with load balancers etc.

        private readonly ConcurrentDictionary<string, string> _connections = new();

        public async Task<HttpResponseMessage> SendHttpRequestAsync(HttpRequestMessage request)
        {
            string connectionId = _connections.FirstOrDefault().Key;
            Log.Information("Sending to: {ConnectionId}", connectionId);
            return await Clients.Client(_connections.FirstOrDefault().Key).HttpRequest(request);
        }


        public Task<bool> Register()
        {            
            if (_connections.TryAdd(Context.ConnectionId, "some-unique-client-id"))
                Log.Information("Client registered: {ConnectionId}", Context.ConnectionId);
            else
                Log.Information("Client already registered: {ConnectionId}", Context.ConnectionId);

            return Task.FromResult(true);
        }
    }
}
