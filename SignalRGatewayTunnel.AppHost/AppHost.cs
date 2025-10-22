var builder = DistributedApplication.CreateBuilder(args);

var frontend = builder.AddProject<Projects.Frontend>("frontend")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health");

builder.AddProject<Projects.Backend>("backend")
    .WithHttpHealthCheck("/health")
    .WithReference(frontend)
    .WaitFor(frontend);

builder.Build().Run();
