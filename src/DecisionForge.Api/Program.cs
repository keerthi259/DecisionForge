using DecisionForge.Api.Configuration;
using DecisionForge.Api.Correlation;
using DecisionForge.Api.Foundation;
using DecisionForge.Api.Foundation.Idempotency;
using DecisionForge.Api.Identity;
using DecisionForge.Api.Operations;
using DecisionForge.Infrastructure;
using DecisionForge.ServiceDefaults;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddDecisionForgePlatform(builder.Configuration);
builder.Services.AddDecisionForgeInfrastructure(builder.Configuration);
builder.Services.AddDecisionForgeApiFoundation(builder.Configuration);
builder.Services.AddDecisionForgeIdentityApi(builder.Configuration);
builder.AddNpgsqlDataSource("decisionforge");

WebApplication app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseMiddleware<CorrelationMiddleware>();
app.UseExceptionHandler();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseMiddleware<RequestBodyLimitMiddleware>();
app.UseStatusCodePages(ApiStatusCodePages.WriteAsync);
app.UseCors(ApiFoundationServiceCollectionExtensions.CorsPolicyName);
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.UseAntiforgery();
app.UseMiddleware<IdempotencyMiddleware>();
app.MapOperationalEndpoints();
RouteGroupBuilder apiVersionOne = app.MapApiVersionOne();
apiVersionOne.MapDecisionForgeIdentityEndpoints();
app.MapOpenApi("/api/v1/openapi/{documentName}.json");

app.Run();

public partial class Program;
