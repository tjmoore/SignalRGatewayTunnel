# SignalRGatewayTunnel

Sample SignalR based API Gateway tunnel

WORK IN PROGRESS


This is a rough proof of concept of an API Gateway tunnel using SignalR. This shouldn't be relied upon as production ready, nor expect to be stable or secure.

The use case is for applications that reside in an on-premise or locked-down environment where it is not possible to open ports to expose the application endpoints. An application outside of that environment may need to connect to the application inside.

Here a Backend service would run inside the locked environment, and a Frontend service runs externally and exposes a SignalR endpoint to listen on. The Backend connects to the Frontend and registers itself.

The Frontend accepts HTTP requests, packages these into messages and sends to the SignalR client (the Backend), which then unpacks and sends an HTTP request inside the locked environment to an internal endpoint. The HTTP response is packaged and returned to the Backend service.

While this kind of set up is also achievable with VPNs, SSH tunnels and similar, this provides a framework for a custom tunnel or reverse proxy that could be made to handle many client applications. For example a single endpoint externally that an application or users can access, with routing to multiple backends depending on some identifier in the request, DNS, etc.

This doesn't handle tunneling SignalR itself at present.

The example here currently only routes to the first client that registers.

Dockerfile configs are just defaults generated for the projects and untested.


## References

General SignalR and retry logic https://learn.microsoft.com/en-us/aspnet/core/signalr/dotnet-client?view=aspnetcore-8.0&tabs=visual-studio

Loosely based Frontend middleware on https://auth0.com/blog/building-a-reverse-proxy-in-dot-net-core/ but using SignalR instead

## Dependencies

Projects are targetting .NET 8, but minimum .NET 7 for SignalR Client Results support.

MessagePack for fast binary package transport https://github.com/MessagePack-CSharp/MessagePack-CSharp