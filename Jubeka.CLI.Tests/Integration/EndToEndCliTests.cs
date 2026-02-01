using System.Net;
using System.Net.Sockets;
using System.Text;
using Jubeka.CLI;

namespace Jubeka.CLI.Tests.Integration;

[Collection("ConsoleTests")]
public class EndToEndCliTests : IAsyncLifetime
{
    private readonly string _tempHome = Path.Combine(Path.GetTempPath(), $"jubeka-cli-int-{Guid.NewGuid():N}");
    private string? _originalHome;

    public Task InitializeAsync()
    {
        _originalHome = Environment.GetEnvironmentVariable("HOME");
        Directory.CreateDirectory(_tempHome);
        Environment.SetEnvironmentVariable("HOME", _tempHome);
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        Environment.SetEnvironmentVariable("HOME", _originalHome);
        if (Directory.Exists(_tempHome))
        {
            try
            {
                Directory.Delete(_tempHome, true);
            }
            catch
            {
                // Best-effort cleanup
            }
        }

        return Task.CompletedTask;
    }

    [Fact]
    public async Task EndToEnd_Workflow_CoversRequestAndOpenApiCommands()
    {
        await using TestHttpServer server = await TestHttpServer.StartAsync();
        string envName = $"TestEnv-{Guid.NewGuid():N}";
        string varsPath = Path.Combine(_tempHome, "vars.yml");
        File.WriteAllText(varsPath, "variables:\n  token: abc\n");

        string specPath = Path.Combine(_tempHome, "openapi.yaml");
        File.WriteAllText(specPath, $"openapi: 3.0.1\ninfo:\n  title: Test API\n  version: '1.0'\nservers:\n  - url: {server.BaseAddress.TrimEnd('/')}\npaths:\n  /api/breeds/list/all:\n    get:\n      operationId: listBreeds\n      responses:\n        '200':\n          description: ok\n");

        string requestUrl = $"{server.BaseAddress}api/breeds/list/all";
        Cli cli = Startup.CreateCli();

        var result = await RunCliAsync(cli, ["env", "create", "--name", envName, "--vars", varsPath, "--spec-file", specPath]);
        AssertSuccess(result);
        Assert.Contains($"Environment '{envName}' created.", result.Output, StringComparison.Ordinal);

        result = await RunCliAsync(cli, ["env", "set", "--name", envName]);
        AssertSuccess(result);
        Assert.Contains($"Current environment set to '{envName}'.", result.Output, StringComparison.Ordinal);

        result = await RunCliAsync(cli, ["env", "edit", "--name", envName, "--inline", "--vars", varsPath]);
        AssertSuccess(result);
        Assert.Contains($"Environment '{envName}' updated.", result.Output, StringComparison.Ordinal);

        result = await RunCliAsync(cli, ["request", "add", "--name", envName, "--req-name", "dogs", "--method", "GET", "--url", requestUrl]);
        AssertSuccess(result);
        Assert.Contains($"Request 'dogs' added to '{envName}'.", result.Output, StringComparison.Ordinal);

        result = await RunCliAsync(cli, ["request", "list", "--name", envName]);
        AssertSuccess(result);
        Assert.Contains("dogs [GET]", result.Output, StringComparison.Ordinal);

        result = await RunCliAsync(cli, ["request", "edit", "--name", envName, "--req-name", "dogs", "--inline", "--method", "POST"]);
        AssertSuccess(result);
        Assert.Contains("Request 'dogs' updated", result.Output, StringComparison.Ordinal);

        result = await RunCliAsync(cli, ["request", "list", "--name", envName]);
        AssertSuccess(result);
        Assert.Contains("dogs [POST]", result.Output, StringComparison.Ordinal);

        result = await RunCliAsync(cli, ["request", "edit", "--name", envName, "--req-name", "dogs", "--inline", "--method", "GET"]);
        AssertSuccess(result);
        Assert.Contains("Request 'dogs' updated", result.Output, StringComparison.Ordinal);

        result = await RunCliAsync(cli, ["request", "exec", "--name", envName, "--req-name", "dogs", "--timeout", "5"]);
        AssertSuccess(result);
        Assert.Contains("HTTP 200", result.Output, StringComparison.Ordinal);
        Assert.Contains("message", result.Output, StringComparison.Ordinal);
        Assert.Contains("status", result.Output, StringComparison.Ordinal);

        result = await RunCliAsync(cli, ["request", "--method", "GET", "--url", requestUrl, "--env", varsPath, "--timeout", "5"]);
        AssertSuccess(result);
        Assert.Contains("HTTP 200", result.Output, StringComparison.Ordinal);

        result = await RunCliAsync(cli, ["openapi", "request", "--operation", "listBreeds", "--spec-file", specPath, "--env", varsPath, "--timeout", "5"]);
        AssertSuccess(result);
        Assert.Contains("HTTP 200", result.Output, StringComparison.Ordinal);

        result = await RunCliAsync(cli, ["-h"]);
        AssertSuccess(result);
        Assert.Contains("Jubeka CLI - REST client", result.Output, StringComparison.Ordinal);
    }

    private static async Task<(int Code, string Output)> RunCliAsync(Cli cli, IReadOnlyList<string> args)
    {
        TextWriter originalOut = Console.Out;
        TextWriter originalErr = Console.Error;
        using StringWriter output = new();
        using StringWriter error = new();

        try
        {
            Console.SetOut(output);
            Console.SetError(error);
            int code = await cli.RunAsync([.. args], CancellationToken.None);
            output.Flush();
            error.Flush();
            return (code, output.ToString() + error.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
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

                string payload = "{\"message\":\"ok\",\"status\":\"success\"}";
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

    private static void AssertSuccess((int Code, string Output) result)
    {
        Assert.True(result.Code == 0, result.Output);
    }
}
