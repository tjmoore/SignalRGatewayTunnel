# SignalRGatewayTunnel

[![Build](https://github.com/tjmoore/SignalRGatewayTunnel/actions/workflows/build.yml/badge.svg)](https://github.com/tjmoore/SignalRGatewayTunnel/actions/workflows/build.yml)

Sample SignalR based Gateway tunnel

This is a rough proof of concept of a Gateway tunnel using SignalR

**This shouldn't be relied upon as production ready, nor expect to be stable or secure**

## Components

This example consists of the following components:

<img src="Design/SignalRGateway Components.svg" alt="Component Structure" width="700">

- External Client (browser or other HTTP client)
- Frontend Proxy (cloud hosted for example)
- Backend Gateway (inside a restricted network for example)
- Destination Backend Service (the destination service to be accessed)

## Sequence

- Frontend Proxy hosts a SignalR Hub that the Backend Gateway connects to as a client
- Backend Gateway registers itself with the Frontend Proxy, and listens for incoming requests coming back on the SignalR connection
- When a request is received by the Frontend Proxy, it packages the request into a message and sends it to the Backend Gateway via SignalR
- Backend Gateway then unpacks the message, makes the HTTP request to the Destination Backend Service, and sends the response back to the Frontend Proxy, which then returns it to the original client

<img src="Design/SignalRGateway Sequence.svg" alt="UML Sequence Diagram" width="900">

## Use Case

A use case may be for example, a corporate network that restricts inbound traffic, but allows outbound HTTPS traffic to the internet. They may not wish to open firewalls and provide routing to the internal service.
In this case, a Backend Gateway service runs inside the corporate network can connect out to a Frontend service hosted in the cloud, which then allows external clients to access the internal service via the Frontend Proxy.

Likewise useful for home environments for a service hosted in a NAS for example, that needs to be accessed externally without opening up home network firewalls.

## Notes

This isn't intended as a backdoor or way to bypass security, but rather a framework to allow controlled access to internal services without opening up firewalls or exposing services directly to the internet.

Noting the security implications of exposing internal services externally, this should be done with care, and appropriate security measures in place.

**This example does not include any authentication or encryption other than what comes out of the box (HTTPS support for example)**

While this kind of set up is also achievable with VPNs, SSH tunnels and similar, this provides a framework for a custom tunnel or reverse proxy that could be made to handle many client applications. For example a single endpoint externally that an application or users can access, with routing to multiple backends depending on some identifier in the request, DNS, etc.

Tunnelling SignalR within the SignalR connection is not support/untested. Although potentially long-polling SignalR (HTTP) requests might work, but again not tested.

The example here currently only routes to the first client that registers.

## Component technologies

* .NET 9 (should run on .NET 8 also, but targets 9 in the example projects)
* SignalR
* MessagePack for fast binary package transport https://github.com/MessagePack-CSharp/MessagePack-CSharp
* Aspire .NET to orchestrate the environment and aid development and debug

## Running development environment

#### Visual Studio
Set `AppHost` as start up project and run (F5)

#### Visual Studio Code
From Solution Explorer, right click `AppHost` and select Debug -> Start New Instance

#### Command Line
Run `dotnet run --project AppHost`

You may have to select the dashboard link shown in the console output to launch in the browser

This will run the .NET Aspire host, launching the components and dashboard in the browser showing the service status.

If the Backend Gateway has connected to the frontend successfully, browse to the Frontend Proxy URL (http://localhost:5105 or https://localhost:7175).
This will send the HTTP request in the browser via the gateway to the destination endpoint at http://localhost:5174 and return the response or timeout if destination is not running.

## Configuration

`launchSettings.json` in each project defines config for ports etc to launch each app. `AppHost` defines the development environment.

`Backend\appsettings.*.json` defines the destination endpoint to forward inbound requests to, and the tunnel endpoint on the frontend to register with.


## References

General SignalR and retry logic https://learn.microsoft.com/en-us/aspnet/core/signalr/dotnet-client?view=aspnetcore-8.0&tabs=visual-studio

Loosely based Frontend middleware on https://auth0.com/blog/building-a-reverse-proxy-in-dot-net-core/ but using SignalR instead
