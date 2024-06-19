using Microsoft.AspNetCore.SignalR;
using Model;
using Serilog;
using System.Collections.Concurrent;

namespace Frontend.Hubs
{
    public interface ITunnel
    {
        Task<ResponseMessage> HttpRequest(RequestMessage request);
    }

    public class TunnelHub : Hub<ITunnel>
    {
        private readonly ConcurrentDictionary<string, string> _connections = new();

        public async Task<ResponseMessage> SendHttpRequestAsync(RequestMessage request)
        {
            // TODO: target/routing. Currently just sends to first client

            string connectionId = _connections.FirstOrDefault().Value;
            Log.Debug("Sending to: {ConnectionId}", connectionId);
            return await Clients.Client(_connections.FirstOrDefault().Value).HttpRequest(request);
        }


        public Task<bool> Register(string clientId)
        {
            _connections.AddOrUpdate(clientId.ToString(), Context.ConnectionId, (k, v) => Context.ConnectionId);
            Log.Information("Client registered: {ClientId} - {ConnectionId}", clientId, Context.ConnectionId);

            return Task.FromResult(true);
        }
    }
}
