using System.Net;
using System.Net.Sockets;
using System.Text;
using Jubeka.CLI.Application;
using Jubeka.CLI.Application.Default;
using Jubeka.CLI.Domain;
using Jubeka.CLI.Infrastructure.Config;
using Jubeka.Core.Application;
using Jubeka.Core.Application.Default;
using Jubeka.Core.Domain;
using Jubeka.Core.Infrastructure.IO;
using Microsoft.OpenApi.Models;

namespace Jubeka.CLI.Tests;

[Collection("ConsoleTests")]
public class CLITests
{
    [Fact]
    public async Task RunAsync_WhenHelpRequested_ReturnsHelpPrinterCode()
    {
        StubHelpPrinter helpPrinter = new(42);
        StubArgumentParser parser = new(ParseResult.Help("invalid"));
        ThrowingRequestDataBuilder requestBuilder = new();
        ThrowingResponseWriter responseWriter = new();

        StubEnvironmentVariablesLoader envLoader = new();
        ThrowingOpenApiSpecLoader specLoader = new();
        ThrowingOpenApiRequestBuilder openApiRequestBuilder = new();
        StubEnvironmentConfigStore envStore = new();
        ThrowingEnvironmentWizard envWizard = new();
        ThrowingRequestWizard requestWizard = new();

        Cli cli = new(helpPrinter, parser, requestBuilder, responseWriter, envLoader, specLoader, openApiRequestBuilder, envStore, envWizard, requestWizard);

        int result = await cli.RunAsync(["--help"], CancellationToken.None);

        Assert.Equal(42, result);
        Assert.Equal("invalid", helpPrinter.LastError);
    }

    [Fact]
    public async Task RunAsync_EnvCreate_WritesConfig()
    {
        using TempHomeScope home = new();
        string varsPath = Path.Combine(home.HomePath, "vars.yml");
        File.WriteAllText(varsPath, "variables:\n  token: abc\n");

        EnvironmentConfigStore envStore = new();
        EnvConfigOptions options = new("dev", varsPath, null);
        ParseResult parseResult = ParseResult.Success(CliCommand.EnvCreate, options);

        Cli cli = CreateCli(parseResult, envStore);
        int result = await cli.RunAsync(["env", "create"], CancellationToken.None);

        Assert.Equal(0, result);
        Assert.NotNull(envStore.Get("dev"));
    }

    [Fact]
    public async Task RunAsync_EnvUpdate_WritesConfig()
    {
        using TempHomeScope home = new();
        string varsPath = Path.Combine(home.HomePath, "vars.yml");
        File.WriteAllText(varsPath, "variables:\n  token: abc\n");

        EnvironmentConfigStore envStore = new();
        envStore.Save(new EnvironmentConfig("dev", varsPath, null, []));

        string newVars = Path.Combine(home.HomePath, "vars2.yml");
        File.WriteAllText(newVars, "variables:\n  token: def\n");
        EnvConfigOptions options = new("dev", newVars, null);
        ParseResult parseResult = ParseResult.Success(CliCommand.EnvUpdate, options);

        Cli cli = CreateCli(parseResult, envStore);
        int result = await cli.RunAsync(["env", "update"], CancellationToken.None);

        Assert.Equal(0, result);
        EnvironmentConfig? updated = envStore.Get("dev");
        Assert.NotNull(updated);
    }

    [Fact]
    public async Task RunAsync_EnvEditInline_UpdatesConfig()
    {
        using TempHomeScope home = new();
        string varsPath = Path.Combine(home.HomePath, "vars.yml");
        File.WriteAllText(varsPath, "variables:\n  token: abc\n");

        EnvironmentConfigStore envStore = new();
        envStore.Save(new EnvironmentConfig("dev", varsPath, null, []));

        string newVars = Path.Combine(home.HomePath, "vars2.yml");
        File.WriteAllText(newVars, "variables:\n  token: def\n");
        EnvEditOptions options = new("dev", newVars, null, true);
        ParseResult parseResult = ParseResult.Success(CliCommand.EnvEdit, options);

        Cli cli = CreateCli(parseResult, envStore);
        int result = await cli.RunAsync(["env", "edit"], CancellationToken.None);

        Assert.Equal(0, result);
        EnvironmentConfig? updated = envStore.Get("dev");
        Assert.NotNull(updated);
    }

    [Fact]
    public async Task RunAsync_EnvSet_SetsCurrent()
    {
        using TempHomeScope home = new();
        string varsPath = Path.Combine(home.HomePath, "vars.yml");
        File.WriteAllText(varsPath, "variables:\n  token: abc\n");

        EnvironmentConfigStore envStore = new();
        envStore.Save(new EnvironmentConfig("dev", varsPath, null, []));

        EnvSetOptions options = new("dev");
        ParseResult parseResult = ParseResult.Success(CliCommand.EnvSet, options);

        Cli cli = CreateCli(parseResult, envStore);
        int result = await cli.RunAsync(["env", "set"], CancellationToken.None);

        Assert.Equal(0, result);
        Assert.Equal("dev", envStore.GetCurrent());
    }

    [Fact]
    public async Task RunAsync_EnvDelete_RemovesConfig()
    {
        using TempHomeScope home = new();
        string varsPath = Path.Combine(home.HomePath, "vars.yml");
        File.WriteAllText(varsPath, "variables:\n  token: abc\n");

        EnvironmentConfigStore envStore = new();
        envStore.Save(new EnvironmentConfig("dev", varsPath, null, []));

        EnvDeleteOptions options = new("dev");
        ParseResult parseResult = ParseResult.Success(CliCommand.EnvDelete, options);

        Cli cli = CreateCli(parseResult, envStore);
        int result = await cli.RunAsync(["env", "delete"], CancellationToken.None);

        Assert.Equal(0, result);
        Assert.Null(envStore.Get("dev"));
    }

    [Fact]
    public async Task RunAsync_RequestAdd_AddsRequest()
    {
        using TempHomeScope home = new();
        string varsPath = Path.Combine(home.HomePath, "vars.yml");
        File.WriteAllText(varsPath, "variables:\n");

        EnvironmentConfigStore envStore = new();
        envStore.Save(new EnvironmentConfig("dev", varsPath, null, []));

        EnvRequestAddOptions options = new("dev", "Ping", "GET", "https://example.com", null, [], []);
        ParseResult parseResult = ParseResult.Success(CliCommand.RequestAdd, options);

        Cli cli = CreateCli(parseResult, envStore);
        int result = await cli.RunAsync(["request", "add"], CancellationToken.None);

        Assert.Equal(0, result);
        Assert.Contains(envStore.Get("dev")!.Requests, r => r.Name == "Ping");
    }

    [Fact]
    public async Task RunAsync_RequestList_Works()
    {
        using TempHomeScope home = new();
        string varsPath = Path.Combine(home.HomePath, "vars.yml");
        File.WriteAllText(varsPath, "variables:\n");

        EnvironmentConfigStore envStore = new();
        envStore.Save(new EnvironmentConfig("dev", varsPath, null, [
            new RequestDefinition("Ping", "GET", "https://example.com", null, [], [], new AuthConfig(AuthMethod.Inherit))
        ]));

        EnvRequestListOptions options = new("dev");
        ParseResult parseResult = ParseResult.Success(CliCommand.RequestList, options);

        Cli cli = CreateCli(parseResult, envStore);
        int result = await cli.RunAsync(["request", "list"], CancellationToken.None);

        Assert.Equal(0, result);
    }

    [Fact]
    public async Task RunAsync_RequestEdit_UpdatesRequest()
    {
        using TempHomeScope home = new();
        string varsPath = Path.Combine(home.HomePath, "vars.yml");
        File.WriteAllText(varsPath, "variables:\n");

        EnvironmentConfigStore envStore = new();
        envStore.Save(new EnvironmentConfig("dev", varsPath, null, [
            new RequestDefinition("Ping", "GET", "https://example.com", null, [], [], new AuthConfig(AuthMethod.Inherit))
        ]));

        EnvRequestEditOptions options = new("dev", "Ping", true, "POST", null, null, [], []);
        ParseResult parseResult = ParseResult.Success(CliCommand.RequestEdit, options);

        Cli cli = CreateCli(parseResult, envStore);
        int result = await cli.RunAsync(["request", "edit"], CancellationToken.None);

        Assert.Equal(0, result);
        RequestDefinition updated = Assert.Single(envStore.Get("dev")!.Requests, r => r.Name == "Ping");
        Assert.Equal("POST", updated.Method);
    }

    [Fact]
    public async Task RunAsync_RequestExec_SendsRequest()
    {
        using TempHomeScope home = new();
        await using TestHttpServer server = await TestHttpServer.StartAsync();

        string varsPath = Path.Combine(home.HomePath, "vars.yml");
        File.WriteAllText(varsPath, "variables:\n");

        EnvironmentConfigStore envStore = new();
        envStore.Save(new EnvironmentConfig("dev", varsPath, null, [
            new RequestDefinition("Ping", "GET", server.BaseAddress + "ping", null, [], [], new AuthConfig(AuthMethod.Inherit))
        ]));

        EnvRequestExecOptions options = new("dev", "Ping", 5);
        ParseResult parseResult = ParseResult.Success(CliCommand.RequestExec, options);

        StubResponseWriter responseWriter = new();
        Cli cli = CreateCli(parseResult, envStore, responseWriter: responseWriter, requestDataBuilder: CreateRequestDataBuilder(), envLoader: new EnvironmentVariablesLoader());
        int result = await cli.RunAsync(["request", "exec"], CancellationToken.None);

        Assert.Equal(0, result);
        Assert.NotNull(responseWriter.LastResponse);
    }

    [Fact]
    public async Task RunAsync_Request_SendsRequest()
    {
        await using TestHttpServer server = await TestHttpServer.StartAsync();
        ParseResult parseResult = ParseResult.Success(
            CliCommand.Request,
            new RequestCommandOptions("GET", server.BaseAddress + "ping", null, null, 5, false, [], []));

        StubResponseWriter responseWriter = new();
        Cli cli = CreateCli(parseResult, new EnvironmentConfigStore(), responseWriter: responseWriter, requestDataBuilder: CreateRequestDataBuilder(), envLoader: new EnvironmentVariablesLoader());
        int result = await cli.RunAsync(["request"], CancellationToken.None);

        Assert.Equal(0, result);
        Assert.NotNull(responseWriter.LastResponse);
    }

    [Fact]
    public async Task RunAsync_OpenApiRequest_SendsRequest()
    {
        using TempHomeScope home = new();
        await using TestHttpServer server = await TestHttpServer.StartAsync();

        string varsPath = Path.Combine(home.HomePath, "vars.yml");
        File.WriteAllText(varsPath, "variables:\n");

        OpenApiCommandOptions options = new(
            "getPing",
            new OpenApiSource(OpenApiSourceKind.Raw, "openapi: 3.0.0"),
            varsPath,
            null,
            5,
            false);
        ParseResult parseResult = ParseResult.Success(CliCommand.OpenApiRequest, options);

        StubResponseWriter responseWriter = new();
        StubOpenApiSpecLoader specLoader = new();
        StubOpenApiRequestBuilder requestBuilder = new(new RequestOptions("GET", server.BaseAddress + "ping", null, [], []));

        Cli cli = CreateCli(parseResult, new EnvironmentConfigStore(), responseWriter: responseWriter, requestDataBuilder: CreateRequestDataBuilder(), envLoader: new EnvironmentVariablesLoader(), openApiSpecLoader: specLoader, openApiRequestBuilder: requestBuilder);
        int result = await cli.RunAsync(["openapi", "request"], CancellationToken.None);

        Assert.Equal(0, result);
        Assert.NotNull(responseWriter.LastResponse);
    }

    [Fact]
    public async Task RunAsync_HelpCommand_Request_Works()
    {
        await using TestHttpServer server = await TestHttpServer.StartAsync();
        StubResponseWriter responseWriter = new();
        Cli cli = CreateCliWithParser(
            new ArgumentParser(),
            new EnvironmentConfigStore(),
            responseWriter: responseWriter,
            requestDataBuilder: CreateRequestDataBuilder(),
            envLoader: new EnvironmentVariablesLoader());

        int result = await cli.RunAsync([
            "request",
            "--method", "GET",
            "--url", server.BaseAddress + "ping",
            "--timeout", "5"], CancellationToken.None);

        Assert.Equal(0, result);
        Assert.NotNull(responseWriter.LastResponse);
    }

    [Fact]
    public async Task RunAsync_HelpCommand_RequestAdd_Works()
    {
        using TempHomeScope home = new();
        string varsPath = Path.Combine(home.HomePath, "vars.yml");
        File.WriteAllText(varsPath, "variables:\n");

        EnvironmentConfigStore envStore = new();
        envStore.Save(new EnvironmentConfig("dev", varsPath, null, []));

        Cli cli = CreateCliWithParser(new ArgumentParser(), envStore);
        int result = await cli.RunAsync([
            "request",
            "add",
            "--name", "dev",
            "--req-name", "Ping",
            "--method", "GET",
            "--url", "https://example.com"], CancellationToken.None);

        Assert.Equal(0, result);
        Assert.Contains(envStore.Get("dev")!.Requests, r => r.Name == "Ping");
    }

    [Fact]
    public async Task RunAsync_HelpCommand_RequestList_Works()
    {
        using TempHomeScope home = new();
        string varsPath = Path.Combine(home.HomePath, "vars.yml");
        File.WriteAllText(varsPath, "variables:\n");

        EnvironmentConfigStore envStore = new();
        envStore.Save(new EnvironmentConfig("dev", varsPath, null, [
            new RequestDefinition("Ping", "GET", "https://example.com", null, [], [], new AuthConfig(AuthMethod.Inherit))
        ]));

        Cli cli = CreateCliWithParser(new ArgumentParser(), envStore);
        int result = await cli.RunAsync([
            "request",
            "list",
            "--name", "dev"], CancellationToken.None);

        Assert.Equal(0, result);
    }

    [Fact]
    public async Task RunAsync_HelpCommand_RequestEdit_Works()
    {
        using TempHomeScope home = new();
        string varsPath = Path.Combine(home.HomePath, "vars.yml");
        File.WriteAllText(varsPath, "variables:\n");

        EnvironmentConfigStore envStore = new();
        envStore.Save(new EnvironmentConfig("dev", varsPath, null, [
            new RequestDefinition("Ping", "GET", "https://example.com", null, [], [], new AuthConfig(AuthMethod.Inherit))
        ]));

        Cli cli = CreateCliWithParser(new ArgumentParser(), envStore);
        int result = await cli.RunAsync([
            "request",
            "edit",
            "--name", "dev",
            "--req-name", "Ping",
            "--inline",
            "--method", "POST"], CancellationToken.None);

        Assert.Equal(0, result);
        RequestDefinition updated = Assert.Single(envStore.Get("dev")!.Requests, r => r.Name == "Ping");
        Assert.Equal("POST", updated.Method);
    }

    [Fact]
    public async Task RunAsync_HelpCommand_RequestExec_Works()
    {
        using TempHomeScope home = new();
        await using TestHttpServer server = await TestHttpServer.StartAsync();

        string varsPath = Path.Combine(home.HomePath, "vars.yml");
        File.WriteAllText(varsPath, "variables:\n");

        EnvironmentConfigStore envStore = new();
        envStore.Save(new EnvironmentConfig("dev", varsPath, null, [
            new RequestDefinition("Ping", "GET", server.BaseAddress + "ping", null, [], [], new AuthConfig(AuthMethod.Inherit))
        ]));

        StubResponseWriter responseWriter = new();
        Cli cli = CreateCliWithParser(
            new ArgumentParser(),
            envStore,
            responseWriter: responseWriter,
            requestDataBuilder: CreateRequestDataBuilder(),
            envLoader: new EnvironmentVariablesLoader());

        int result = await cli.RunAsync([
            "request",
            "exec",
            "--name", "dev",
            "--req-name", "Ping",
            "--timeout", "5"], CancellationToken.None);

        Assert.Equal(0, result);
        Assert.NotNull(responseWriter.LastResponse);
    }

    [Fact]
    public async Task RunAsync_HelpCommand_OpenApiRequest_Works()
    {
        using TempHomeScope home = new();
        await using TestHttpServer server = await TestHttpServer.StartAsync();

        string varsPath = Path.Combine(home.HomePath, "vars.yml");
        File.WriteAllText(varsPath, "variables:\n");

        StubResponseWriter responseWriter = new();
        StubOpenApiSpecLoader specLoader = new();
        StubOpenApiRequestBuilder requestBuilder = new(new RequestOptions("GET", server.BaseAddress + "ping", null, [], []));

        Cli cli = CreateCliWithParser(
            new ArgumentParser(),
            new EnvironmentConfigStore(),
            responseWriter: responseWriter,
            requestDataBuilder: CreateRequestDataBuilder(),
            envLoader: new EnvironmentVariablesLoader(),
            openApiSpecLoader: specLoader,
            openApiRequestBuilder: requestBuilder);

        int result = await cli.RunAsync([
            "openapi",
            "request",
            "--operation", "getPing",
            "--spec-raw", "openapi: 3.0.0",
            "--env", varsPath], CancellationToken.None);

        Assert.Equal(0, result);
        Assert.NotNull(responseWriter.LastResponse);
    }

    [Fact]
    public async Task RunAsync_HelpCommand_EnvCreate_Works()
    {
        using TempHomeScope home = new();
        string varsPath = Path.Combine(home.HomePath, "vars.yml");
        File.WriteAllText(varsPath, "variables:\n");

        EnvironmentConfigStore envStore = new();
        Cli cli = CreateCliWithParser(new ArgumentParser(), envStore);

        int result = await cli.RunAsync([
            "env",
            "create",
            "--name", "dev",
            "--vars", varsPath], CancellationToken.None);

        Assert.Equal(0, result);
        Assert.NotNull(envStore.Get("dev"));
    }

    [Fact]
    public async Task RunAsync_HelpCommand_EnvUpdate_Works()
    {
        using TempHomeScope home = new();
        string varsPath = Path.Combine(home.HomePath, "vars.yml");
        File.WriteAllText(varsPath, "variables:\n");

        EnvironmentConfigStore envStore = new();
        envStore.Save(new EnvironmentConfig("dev", varsPath, null, []));

        string newVars = Path.Combine(home.HomePath, "vars2.yml");
        File.WriteAllText(newVars, "variables:\n");

        Cli cli = CreateCliWithParser(new ArgumentParser(), envStore);
        int result = await cli.RunAsync([
            "env",
            "update",
            "--name", "dev",
            "--vars", newVars], CancellationToken.None);

        Assert.Equal(0, result);
        Assert.NotNull(envStore.Get("dev"));
    }

    [Fact]
    public async Task RunAsync_HelpCommand_EnvEdit_Works()
    {
        using TempHomeScope home = new();
        string varsPath = Path.Combine(home.HomePath, "vars.yml");
        File.WriteAllText(varsPath, "variables:\n");

        EnvironmentConfigStore envStore = new();
        envStore.Save(new EnvironmentConfig("dev", varsPath, null, []));

        string newVars = Path.Combine(home.HomePath, "vars2.yml");
        File.WriteAllText(newVars, "variables:\n");

        Cli cli = CreateCliWithParser(new ArgumentParser(), envStore);
        int result = await cli.RunAsync([
            "env",
            "edit",
            "--name", "dev",
            "--inline",
            "--vars", newVars], CancellationToken.None);

        Assert.Equal(0, result);
        Assert.NotNull(envStore.Get("dev"));
    }

    [Fact]
    public async Task RunAsync_HelpCommand_EnvSet_Works()
    {
        using TempHomeScope home = new();
        string varsPath = Path.Combine(home.HomePath, "vars.yml");
        File.WriteAllText(varsPath, "variables:\n");

        EnvironmentConfigStore envStore = new();
        envStore.Save(new EnvironmentConfig("dev", varsPath, null, []));

        Cli cli = CreateCliWithParser(new ArgumentParser(), envStore);
        int result = await cli.RunAsync([
            "env",
            "set",
            "--name", "dev"], CancellationToken.None);

        Assert.Equal(0, result);
        Assert.Equal("dev", envStore.GetCurrent());
    }

    [Fact]
    public async Task RunAsync_HelpCommand_EnvDelete_Works()
    {
        using TempHomeScope home = new();
        string varsPath = Path.Combine(home.HomePath, "vars.yml");
        File.WriteAllText(varsPath, "variables:\n");

        EnvironmentConfigStore envStore = new();
        envStore.Save(new EnvironmentConfig("dev", varsPath, null, []));

        Cli cli = CreateCliWithParser(new ArgumentParser(), envStore);
        int result = await cli.RunAsync([
            "env",
            "delete",
            "--name", "dev"], CancellationToken.None);

        Assert.Equal(0, result);
        Assert.Null(envStore.Get("dev"));
    }

    private sealed class StubHelpPrinter(int code) : IHelpPrinter
    {
        public string? LastError { get; private set; }

        public int Print(string? error = null)
        {
            LastError = error;
            return code;
        }
    }

    private sealed class StubArgumentParser(ParseResult result) : IArgumentParser
    {
        public ParseResult Parse(string[] args) => result;
    }

    private sealed class ThrowingHelpPrinter : IHelpPrinter
    {
        public int Print(string? error = null)
        {
            throw new InvalidOperationException("Help printer should not be called in this test.");
        }
    }

    private sealed class ThrowingRequestDataBuilder : IRequestDataBuilder
    {
        public RequestData Build(RequestOptions options, IReadOnlyDictionary<string, string> vars)
        {
            throw new InvalidOperationException("Request builder should not be called for help flow.");
        }
    }

    private sealed class ThrowingResponseWriter : IResponseWriter
    {
        public void Write(ResponseData response, bool pretty)
        {
            throw new InvalidOperationException("Response writer should not be called for help flow.");
        }
    }

    private sealed class StubResponseWriter : IResponseWriter
    {
        public ResponseData? LastResponse { get; private set; }

        public void Write(ResponseData response, bool pretty)
        {
            LastResponse = response;
        }
    }

    private sealed class StubEnvironmentVariablesLoader : IEnvironmentVariablesLoader
    {
        public IReadOnlyDictionary<string, string> Load(string? path) => new Dictionary<string, string>();
    }

    private sealed class ThrowingOpenApiSpecLoader : IOpenApiSpecLoader
    {
        public Task<Microsoft.OpenApi.Models.OpenApiDocument> LoadAsync(OpenApiSource source, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("OpenAPI loader should not be called for help flow.");
        }
    }

    private sealed class StubOpenApiSpecLoader : IOpenApiSpecLoader
    {
        public Task<OpenApiDocument> LoadAsync(OpenApiSource source, CancellationToken cancellationToken)
        {
            return Task.FromResult(new OpenApiDocument());
        }
    }

    private sealed class ThrowingOpenApiRequestBuilder : IOpenApiRequestBuilder
    {
        public RequestOptions Build(Microsoft.OpenApi.Models.OpenApiDocument document, string operationId, IReadOnlyDictionary<string, string> vars)
        {
            throw new InvalidOperationException("OpenAPI request builder should not be called for help flow.");
        }
    }

    private sealed class StubOpenApiRequestBuilder(RequestOptions requestOptions) : IOpenApiRequestBuilder
    {
        public RequestOptions Build(OpenApiDocument document, string operationId, IReadOnlyDictionary<string, string> vars)
        {
            return requestOptions;
        }
    }

    private sealed class StubEnvironmentConfigStore : IEnvironmentConfigStore
    {
        public EnvironmentConfig? Get(string name) => null;
        public void Save(EnvironmentConfig config)
        {
            throw new InvalidOperationException("Environment config store should not be called for help flow.");
        }

        public bool Delete(string name)
        {
            throw new InvalidOperationException("Environment config store should not be called for help flow.");
        }

        public string? GetCurrent() => null;

        public void SetCurrent(string name)
        {
            throw new InvalidOperationException("Environment config store should not be called for help flow.");
        }
    }

    private sealed class ThrowingEnvironmentWizard : IEnvironmentWizard
    {
        public EnvironmentConfig BuildEnvironmentConfig(EnvConfigOptions options, string action)
        {
            throw new InvalidOperationException("Environment wizard should not be called for help flow.");
        }
    }

    private sealed class ThrowingRequestWizard : IRequestWizard
    {
        public RequestDefinition BuildRequest(EnvRequestAddOptions options, IReadOnlyDictionary<string, string> vars)
        {
            throw new InvalidOperationException("Request wizard should not be called for help flow.");
        }

        public RequestDefinition EditRequest(RequestDefinition request, IReadOnlyDictionary<string, string> vars)
        {
            throw new InvalidOperationException("Request wizard should not be called for help flow.");
        }

        public int SelectRequestIndex(IReadOnlyList<RequestDefinition> requests, string? requestName)
        {
            throw new InvalidOperationException("Request wizard should not be called for help flow.");
        }
    }

    private static Cli CreateCli(
        ParseResult parseResult,
        IEnvironmentConfigStore envStore,
        IRequestDataBuilder? requestDataBuilder = null,
        IResponseWriter? responseWriter = null,
        IEnvironmentVariablesLoader? envLoader = null,
        IOpenApiSpecLoader? openApiSpecLoader = null,
        IOpenApiRequestBuilder? openApiRequestBuilder = null)
    {
        return new Cli(
            new ThrowingHelpPrinter(),
            new StubArgumentParser(parseResult),
            requestDataBuilder ?? new ThrowingRequestDataBuilder(),
            responseWriter ?? new ThrowingResponseWriter(),
            envLoader ?? new StubEnvironmentVariablesLoader(),
            openApiSpecLoader ?? new ThrowingOpenApiSpecLoader(),
            openApiRequestBuilder ?? new ThrowingOpenApiRequestBuilder(),
            envStore,
            new ThrowingEnvironmentWizard(),
            new ThrowingRequestWizard());
    }

    private static Cli CreateCliWithParser(
        IArgumentParser argumentParser,
        IEnvironmentConfigStore envStore,
        IRequestDataBuilder? requestDataBuilder = null,
        IResponseWriter? responseWriter = null,
        IEnvironmentVariablesLoader? envLoader = null,
        IOpenApiSpecLoader? openApiSpecLoader = null,
        IOpenApiRequestBuilder? openApiRequestBuilder = null)
    {
        return new Cli(
            new ThrowingHelpPrinter(),
            argumentParser,
            requestDataBuilder ?? new ThrowingRequestDataBuilder(),
            responseWriter ?? new ThrowingResponseWriter(),
            envLoader ?? new StubEnvironmentVariablesLoader(),
            openApiSpecLoader ?? new ThrowingOpenApiSpecLoader(),
            openApiRequestBuilder ?? new ThrowingOpenApiRequestBuilder(),
            envStore,
            new ThrowingEnvironmentWizard(),
            new ThrowingRequestWizard());
    }

    private static IRequestDataBuilder CreateRequestDataBuilder()
    {
        IQueryParser queryParser = new QueryParser();
        return new RequestDataBuilder(
            new BodyLoader(),
            new HeaderParser(),
            queryParser,
            new UriBuilderHelper(queryParser));
    }

    private sealed class TempHomeScope : IDisposable
    {
        private readonly string? _originalHome;

        public TempHomeScope()
        {
            _originalHome = Environment.GetEnvironmentVariable("HOME");
            HomePath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(HomePath);
            Environment.SetEnvironmentVariable("HOME", HomePath);
        }

        public string HomePath { get; }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable("HOME", _originalHome);
            if (Directory.Exists(HomePath))
            {
                Directory.Delete(HomePath, true);
            }
        }
    }

    private sealed class TestHttpServer : IAsyncDisposable
    {
        private readonly HttpListener _listener;
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _loop;

        private TestHttpServer(HttpListener listener, string baseAddress)
        {
            _listener = listener;
            BaseAddress = baseAddress;
            _loop = Task.Run(ServeAsync);
        }

        public string BaseAddress { get; }

        public static async Task<TestHttpServer> StartAsync()
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
            return new TestHttpServer(listener, prefix);
        }

        private async Task ServeAsync()
        {
            string payload = "{\"message\":\"ok\",\"status\":\"success\"}";
            byte[] buffer = Encoding.UTF8.GetBytes(payload);

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
}
