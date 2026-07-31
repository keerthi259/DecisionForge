IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

IResourceBuilder<PostgresServerResource> postgres = builder
    .AddPostgres(ResourceNames.Postgres)
    .WithImageTag(ContainerImages.PostgresTag);
IResourceBuilder<PostgresDatabaseResource> database = postgres.AddDatabase(
    ResourceNames.Database,
    "decisionforge");

IResourceBuilder<ContainerResource> mailpit = builder
    .AddContainer(ResourceNames.Mailpit, ContainerImages.Mailpit, ContainerImages.MailpitTag)
    .WithHttpEndpoint(port: 8025, targetPort: 8025, name: "http")
    .WithEndpoint(port: 1025, targetPort: 1025, name: "smtp")
    .WithHttpHealthCheck("/api/v1/info");

IResourceBuilder<ProjectResource> api = builder
    .AddProject<Projects.DecisionForge_Api>(ResourceNames.Api, launchProfileName: "http")
    .WithReference(database)
    .WaitFor(database)
    .WaitFor(mailpit)
    .WithExternalHttpEndpoints();

builder
    .AddViteApp(ResourceNames.Web, "../DecisionForge.Web")
    .WithEndpoint("http", endpoint => endpoint.Port = 5173)
    .WithReference(api)
    .WithEnvironment("DECISIONFORGE_API_TARGET", api.GetEndpoint("http"))
    .WaitFor(api)
    .WithExternalHttpEndpoints();

builder.Build().Run();
