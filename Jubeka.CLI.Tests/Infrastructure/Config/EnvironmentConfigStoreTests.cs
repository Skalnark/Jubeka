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

            string varsPath = Path.Combine(tempHome, "vars.yml");
            File.WriteAllText(varsPath, "variables:\n  token: abc\n");

            EnvironmentConfigStore store = new();
            EnvironmentConfig config = new(
                Name: "dev",
                VarsPath: varsPath,
                DefaultOpenApiSource: new OpenApiSource(OpenApiSourceKind.Url, "https://example.com/openapi.json"),
                Requests: [new RequestDefinition("Ping", "GET", "https://example.com", null, [], [], new AuthConfig(AuthMethod.Inherit))]);

            store.Save(config, local: true, baseDirectory: tempHome);
            EnvironmentConfig? loaded = store.Get("dev", tempHome);

            Assert.NotNull(loaded);
            Assert.Equal("dev", loaded!.Name);
            Assert.Equal(Path.Combine(tempHome, ".jubeka", "dev", "vars.yml"), loaded.VarsPath);
            Assert.NotNull(loaded.DefaultOpenApiSource);
            Assert.NotNull(loaded.Requests);
            Assert.Single(loaded.Requests);
            Assert.True(File.Exists(Path.Combine(tempHome, ".jubeka", "dev", "config.json")));
            Assert.True(File.Exists(Path.Combine(tempHome, ".jubeka", "dev", "vars.yml")));
            Assert.True(File.Exists(Path.Combine(tempHome, ".jubeka", "dev", "openapi.source")));
            Assert.True(File.Exists(Path.Combine(tempHome, ".jubeka", "dev", "requests", "Ping.yml")));
        }
        finally
        {
            Environment.SetEnvironmentVariable("HOME", originalHome);
            if (Directory.Exists(tempHome)) Directory.Delete(tempHome, true);
        }
    }

    [Fact]
    public void SetCurrent_ThenGetCurrent_WorksForGlobalAndLocal()
    {
        string tempHome = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempHome);
        Directory.CreateDirectory(tempDir);
        string? originalHome = Environment.GetEnvironmentVariable("HOME");

        try
        {
            Environment.SetEnvironmentVariable("HOME", tempHome);
            EnvironmentConfigStore store = new();

            store.SetCurrent("global-env");
            Assert.Equal("global-env", store.GetCurrent());

            store.SetCurrent("local-env", true, tempDir);
            Assert.Equal("local-env", store.GetCurrent(tempDir));
        }
        finally
        {
            Environment.SetEnvironmentVariable("HOME", originalHome);
            if (Directory.Exists(tempHome)) Directory.Delete(tempHome, true);
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }
}
