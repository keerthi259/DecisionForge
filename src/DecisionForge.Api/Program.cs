using DecisionForge.Api.Configuration;
using DecisionForge.Api.Correlation;
using DecisionForge.Api.Operations;
using DecisionForge.Infrastructure;
using DecisionForge.ServiceDefaults;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddDecisionForgePlatform(builder.Configuration);
builder.Services.AddDecisionForgeInfrastructure();
builder.AddNpgsqlDataSource("decisionforge");

WebApplication app = builder.Build();

app.UseMiddleware<CorrelationMiddleware>();
app.MapOperationalEndpoints();

app.Run();

public partial class Program;
