using Jubeka.CLI.Infrastructure.Config;
using Jubeka.CLI.Domain;
using Jubeka.Core.Domain;

namespace Jubeka.CLI.Tests.Infrastructure.Config;

public class EnvironmentConfigStoreTests
{
    [Fact]
    public void Save_ThenGet_RoundTripsConfig()
    {
        string tempHome = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempHome);
        string? originalHome = Environment.GetEnvironmentVariable("HOME");

        try
        {
            Environment.SetEnvironmentVariable("HOME", tempHome);

            EnvironmentConfigStore store = new();
            EnvironmentConfig config = new(
                Name: "dev",
                VarsPath: "/tmp/vars.yml",
                DefaultOpenApiSource: new OpenApiSource(OpenApiSourceKind.Url, "https://example.com/openapi.json"),
                Requests: []);

            store.Save(config);
            EnvironmentConfig? loaded = store.Get("dev");

            Assert.NotNull(loaded);
            Assert.Equal("dev", loaded!.Name);
            Assert.Equal("/tmp/vars.yml", loaded.VarsPath);
            Assert.NotNull(loaded.DefaultOpenApiSource);
            Assert.NotNull(loaded.Requests);
        }
        finally
        {
            Environment.SetEnvironmentVariable("HOME", originalHome);
            if (Directory.Exists(tempHome)) Directory.Delete(tempHome, true);
        }
    }
}
