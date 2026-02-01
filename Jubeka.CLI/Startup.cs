using Jubeka.CLI.Application.Default;
using Jubeka.CLI.Infrastructure.Config;
using Jubeka.CLI.Infrastructure.Console;
using Jubeka.CLI.Infrastructure.Formatting;
using Jubeka.Core.Application.Default;
using Jubeka.Core.Infrastructure.IO;
using Jubeka.Core.Infrastructure.OpenApi;

namespace Jubeka.CLI;

public static class Startup
{
    public static Cli CreateCli()
    {
        HelpPrinter helpPrinter = new();
        ArgumentParser parser = new();
        ConsolePrompt prompt = new();
        EnvironmentWizard environmentWizard = new(prompt);
        RequestWizard requestWizard = new(prompt);

        QueryParser queryParser = new();
        HeaderParser headerParser = new();
        UriBuilderHelper uriBuilder = new(queryParser);
        BodyLoader bodyLoader = new();
        EnvironmentVariablesLoader environmentVariablesLoader = new();
        RequestDataBuilder requestBuilder = new(bodyLoader, headerParser, queryParser, uriBuilder);
        ResponseWriter responseWriter = new(new JsonResponseFormatter());
        OpenApiSpecLoader openApiSpecLoader = new();
        OpenApiRequestBuilder openApiRequestBuilder = new();
        EnvironmentConfigStore environmentConfigStore = new();

        return new Cli(
            helpPrinter,
            parser,
            requestBuilder,
            responseWriter,
            environmentVariablesLoader,
            openApiSpecLoader,
            openApiRequestBuilder,
            environmentConfigStore,
            environmentWizard,
            requestWizard);
    }
}
