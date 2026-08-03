using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace DecisionForge.Api.IntegrationTests.Identity;

internal sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
{
    public string EnvironmentName { get; set; } = environmentName;

    public string ApplicationName { get; set; } = "DecisionForge.Api.IntegrationTests";

    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
