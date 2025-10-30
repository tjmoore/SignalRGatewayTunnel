var builder = DistributedApplication.CreateBuilder(args);

var frontend = builder.AddProject<Projects.Frontend>("frontend")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health");

var destination = builder.AddProject<Projects.Destination>("destination")
    .WithHttpHealthCheck("/health");

builder.AddProject<Projects.Backend>("backend")
    .WithHttpHealthCheck("/health")
    .WithReference(frontend)
    .WaitFor(frontend)
    .WithReference(destination)
    .WaitFor(destination);

builder.Build().Run();
