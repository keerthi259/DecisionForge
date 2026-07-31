IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

DistributedApplication application = builder.Build();
application.Run();
