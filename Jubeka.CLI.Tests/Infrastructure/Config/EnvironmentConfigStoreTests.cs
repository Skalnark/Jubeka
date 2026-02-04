using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Jubeka.CLI.Infrastructure.Config;
using Jubeka.CLI.Domain;
using Jubeka.Core.Domain;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Jubeka.CLI.Tests.Infrastructure.Config;

[Collection("ConsoleTests")]
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

            string rawSpec = """
openapi: 3.0.0
info:
    title: Test
    version: "1.0"
servers:
    - url: https://example.com/{version}
      variables:
        version:
          default: v1
paths:
    /ping/{id}:
        get:
            operationId: specPing
            parameters:
                - in: path
                  name: id
                  required: true
                  schema:
                    type: string
                - in: query
                  name: filter
                  schema:
                    type: string
            responses:
                '200':
                    description: ok
    /status:
        get:
            responses:
                '200':
                    description: ok
""";

            EnvironmentConfigStore store = new();
            EnvironmentConfig config = new(
                Name: "dev",
                VarsPath: varsPath,
                DefaultOpenApiSource: new OpenApiSource(OpenApiSourceKind.Raw, rawSpec),
                Requests: [new RequestDefinition("Ping", "GET", "https://example.com", null, [], [], new AuthConfig(AuthMethod.Inherit))]);

            store.Save(config);
            EnvironmentConfig? loaded = store.Get("dev");

            Assert.NotNull(loaded);
            Assert.Equal("dev", loaded!.Name);
            Assert.Equal(Path.Combine(tempHome, ".config", "jubeka", "dev", "vars.yml"), loaded.VarsPath);
            Assert.NotNull(loaded.DefaultOpenApiSource);
            Assert.NotNull(loaded.Requests);
            Assert.Equal(3, loaded.Requests.Count);
            Assert.True(File.Exists(Path.Combine(tempHome, ".config", "jubeka", "dev", "config.json")));
            Assert.True(File.Exists(Path.Combine(tempHome, ".config", "jubeka", "dev", "vars.yml")));
            Assert.True(File.Exists(Path.Combine(tempHome, ".config", "jubeka", "dev", "openapi.json")));
            Assert.True(File.Exists(Path.Combine(tempHome, ".config", "jubeka", "dev", "requests", "Ping.yml")));
            Assert.True(File.Exists(Path.Combine(tempHome, ".config", "jubeka", "dev", "requests", "specPing.yml")));

            string openApiContent = File.ReadAllText(Path.Combine(tempHome, ".config", "jubeka", "dev", "openapi.json"));
            Assert.Contains("openapi: 3.0.0", openApiContent);
            Assert.Contains("specPing", openApiContent);

            List<RequestFileDto> requestFiles = LoadRequestFiles(Path.Combine(tempHome, ".config", "jubeka", "dev", "requests"));
            RequestFileDto specFile = Assert.Single(requestFiles, r => r.Name == "specPing");
            Assert.Equal("GET", specFile.Method);
            Assert.Contains("{{baseUrl}}", specFile.Url);
            Assert.Contains("/ping/", specFile.Url);
            Assert.Contains("{{id}}", specFile.Url);

            RequestFileDto statusFile = Assert.Single(requestFiles, r => r.Name == "/status");
            Assert.Equal("GET", statusFile.Method);

            RequestDefinition specRequest = Assert.Single(loaded.Requests, r => r.Name == "specPing");
            Assert.Contains("{{baseUrl}}", specRequest.Url);
            Assert.Contains("{{id}}", specRequest.Url);
            Assert.Contains("specPing", specRequest.Name);

            RequestDefinition statusRequest = Assert.Single(loaded.Requests, r => r.Name == "/status");
            Assert.DoesNotContain("GET", statusRequest.Name, StringComparison.OrdinalIgnoreCase);

            Dictionary<string, string> vars = LoadVars(Path.Combine(tempHome, ".config", "jubeka", "dev", "vars.yml"));
            Assert.True(vars.ContainsKey("baseUrl"));
            Assert.True(vars.ContainsKey("version"));
            Assert.True(vars.ContainsKey("id"));
            Assert.True(vars.ContainsKey("filter"));
            Assert.Equal("https://example.com/{{version}}", vars["baseUrl"]);
        }
        finally
        {
            Environment.SetEnvironmentVariable("HOME", originalHome);
            if (Directory.Exists(tempHome)) Directory.Delete(tempHome, true);
        }
    }

    [Fact]
    public async Task Save_WithUrlSpec_DownloadsOpenApiJsonAndAddsRequests()
    {
        string tempHome = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempHome);
        string? originalHome = Environment.GetEnvironmentVariable("HOME");

        await using TestOpenApiServer server = await TestOpenApiServer.StartAsync();

        try
        {
            Environment.SetEnvironmentVariable("HOME", tempHome);

            string varsPath = Path.Combine(tempHome, "vars.yml");
            File.WriteAllText(varsPath, "variables:\n");

            EnvironmentConfigStore store = new();
            EnvironmentConfig config = new(
                Name: "dev",
                VarsPath: varsPath,
                DefaultOpenApiSource: new OpenApiSource(OpenApiSourceKind.Url, server.SpecUrl),
                Requests: []);

            store.Save(config);
            EnvironmentConfig? loaded = store.Get("dev");

            Assert.NotNull(loaded);
            Assert.True(File.Exists(Path.Combine(tempHome, ".config", "jubeka", "dev", "openapi.json")));
            Assert.Contains(loaded!.Requests, r => r.Name == "listPets");
            Assert.Contains(loaded.Requests, r => r.Name == "createPet");

            string openApiContent = File.ReadAllText(Path.Combine(tempHome, ".config", "jubeka", "dev", "openapi.json"));
            Assert.Contains("\"openapi\":\"3.0.1\"", openApiContent);
            Assert.Contains("listPets", openApiContent);

            List<RequestFileDto> requestFiles = LoadRequestFiles(Path.Combine(tempHome, ".config", "jubeka", "dev", "requests"));
            RequestFileDto listPets = Assert.Single(requestFiles, r => r.Name == "listPets");
            Assert.Equal("GET", listPets.Method);
            Assert.Contains("{{baseUrl}}", listPets.Url);
            Assert.Contains("/pets/", listPets.Url);
            Assert.Contains("{{petId}}", listPets.Url);
            RequestFileDto createPets = Assert.Single(requestFiles, r => r.Name == "createPet");
            Assert.Equal("POST", createPets.Method);

            Dictionary<string, string> vars = LoadVars(Path.Combine(tempHome, ".config", "jubeka", "dev", "vars.yml"));
            Assert.True(vars.ContainsKey("baseUrl"));
            Assert.True(vars.ContainsKey("petId"));
            Assert.Equal("https://example.com", vars["baseUrl"]);
        }
        finally
        {
            Environment.SetEnvironmentVariable("HOME", originalHome);
            if (Directory.Exists(tempHome)) Directory.Delete(tempHome, true);
        }
    }

    private sealed class TestOpenApiServer : IAsyncDisposable
    {
        private readonly HttpListener _listener;
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _loop;

        private TestOpenApiServer(HttpListener listener, string baseAddress)
        {
            _listener = listener;
            BaseAddress = baseAddress;
            SpecUrl = baseAddress + "openapi.json";
            _loop = Task.Run(ServeAsync);
        }

        public string BaseAddress { get; }

        public string SpecUrl { get; }

        public static async Task<TestOpenApiServer> StartAsync()
        {
            int port;
            using (TcpListener socket = new(IPAddress.Loopback, 0))
            {
                socket.Start();
                port = ((IPEndPoint)socket.LocalEndpoint).Port;
            }

            string prefix = $"http://127.0.0.1:{port}/";
            HttpListener listener = new();
            listener.Prefixes.Add(prefix);
            listener.Start();
            await Task.Yield();
            return new TestOpenApiServer(listener, prefix);
        }

        private async Task ServeAsync()
        {
            const string payload = """
{"openapi":"3.0.1","info":{"title":"Test API","version":"1.0"},"servers":[{"url":"https://example.com"}],"paths":{"/pets/{petId}":{"get":{"operationId":"listPets","parameters":[{"name":"petId","in":"path","required":true,"schema":{"type":"string"}}],"responses":{"200":{"description":"ok"}}},"post":{"operationId":"createPet","parameters":[{"name":"petId","in":"path","required":true,"schema":{"type":"string"}}],"responses":{"201":{"description":"created"}}}}}}
""";

            while (!_cts.IsCancellationRequested)
            {
                HttpListenerContext context;
                try
                {
                    context = await _listener.GetContextAsync().ConfigureAwait(false);
                }
                catch (ObjectDisposedException) when (_cts.IsCancellationRequested)
                {
                    break;
                }
                catch (HttpListenerException) when (_cts.IsCancellationRequested)
                {
                    break;
                }
                catch (HttpListenerException)
                {
                    continue;
                }

                byte[] buffer = Encoding.UTF8.GetBytes(payload);
                context.Response.StatusCode = 200;
                context.Response.ContentType = "application/json";
                context.Response.ContentLength64 = buffer.Length;

                await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length, _cts.Token).ConfigureAwait(false);
                context.Response.Close();
            }
        }

        public async ValueTask DisposeAsync()
        {
            _cts.Cancel();
            _listener.Close();
            try
            {
                await _loop.ConfigureAwait(false);
            }
            catch
            {
                // ignore
            }

            _cts.Dispose();
        }
    }

    private static Dictionary<string, string> LoadVars(string path)
    {
        string yaml = File.ReadAllText(path);
        IDeserializer deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
        EnvironmentYaml? env = deserializer.Deserialize<EnvironmentYaml>(yaml);
        return env?.Variables ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    private static List<RequestFileDto> LoadRequestFiles(string requestsDirectory)
    {
        if (!Directory.Exists(requestsDirectory))
        {
            return [];
        }

        IDeserializer deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        return Directory.GetFiles(requestsDirectory, "*.yml")
            .Select(File.ReadAllText)
            .Select(deserializer.Deserialize<RequestFileDto>)
            .Where(dto => dto != null)
            .ToList()!;
    }

    private sealed class EnvironmentYaml
    {
        public Dictionary<string, string>? Variables { get; init; }
    }

    private sealed class RequestFileDto
    {
        public string Name { get; init; } = string.Empty;
        public string Method { get; init; } = string.Empty;
        public string Url { get; init; } = string.Empty;
    }

    [Fact]
    public void SetCurrent_ThenGetCurrent_WorksForGlobal()
    {
        string tempHome = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempHome);
        string? originalHome = Environment.GetEnvironmentVariable("HOME");

        try
        {
            Environment.SetEnvironmentVariable("HOME", tempHome);
            EnvironmentConfigStore store = new();

            store.SetCurrent("global-env");
            Assert.Equal("global-env", store.GetCurrent());
        }
        finally
        {
            Environment.SetEnvironmentVariable("HOME", originalHome);
            if (Directory.Exists(tempHome)) Directory.Delete(tempHome, true);
        }
    }

    [Fact]
    public void Delete_RemovesEnvAndClearsCurrent()
    {
        string tempHome = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempHome);
        string? originalHome = Environment.GetEnvironmentVariable("HOME");

        try
        {
            Environment.SetEnvironmentVariable("HOME", tempHome);

            string varsPath = Path.Combine(tempHome, "vars.yml");
            File.WriteAllText(varsPath, "variables:\n");

            EnvironmentConfigStore store = new();
            EnvironmentConfig config = new(
                Name: "dev",
                VarsPath: varsPath,
                DefaultOpenApiSource: null,
                Requests: []);

            store.Save(config);
            store.SetCurrent("dev");

            bool deleted = store.Delete("dev");

            Assert.True(deleted);
            Assert.Null(store.Get("dev"));
            Assert.Null(store.GetCurrent());
            Assert.False(Directory.Exists(Path.Combine(tempHome, ".config", "jubeka", "dev")));
        }
        finally
        {
            Environment.SetEnvironmentVariable("HOME", originalHome);
            if (Directory.Exists(tempHome)) Directory.Delete(tempHome, true);
        }
    }
}
