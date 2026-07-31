using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace DecisionForge.Api.IntegrationTests;

internal sealed class ApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting(
            "ConnectionStrings:decisionforge",
            "Host=127.0.0.1;Port=1;Database=decisionforge;Username=test;Password=test;"
                + "Timeout=1;Command Timeout=1;Pooling=false");
    }
}
