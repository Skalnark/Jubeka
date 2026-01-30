using Jubeka.CLI.Application;
using Jubeka.CLI.Domain;
using Jubeka.Core.Application;
using Jubeka.Core.Domain;

namespace Jubeka.CLI.Tests;

public class CliTests
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

        Cli cli = new(helpPrinter, parser, requestBuilder, responseWriter, envLoader, specLoader, openApiRequestBuilder, envStore);

        int result = await cli.RunAsync(["--help"], CancellationToken.None);

        Assert.Equal(42, result);
        Assert.Equal("invalid", helpPrinter.LastError);
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

    private sealed class ThrowingOpenApiRequestBuilder : IOpenApiRequestBuilder
    {
        public RequestOptions Build(Microsoft.OpenApi.Models.OpenApiDocument document, string operationId, IReadOnlyDictionary<string, string> vars)
        {
            throw new InvalidOperationException("OpenAPI request builder should not be called for help flow.");
        }
    }

    private sealed class StubEnvironmentConfigStore : IEnvironmentConfigStore
    {
        public EnvironmentConfig? Get(string name, string? baseDirectory = null) => null;
        public void Save(EnvironmentConfig config, string? baseDirectory = null)
        {
            throw new InvalidOperationException("Environment config store should not be called for help flow.");
        }

        public string? GetCurrent(string? baseDirectory = null) => null;

        public void SetCurrent(string name, string? baseDirectory = null)
        {
            throw new InvalidOperationException("Environment config store should not be called for help flow.");
        }
    }
}
