using Jubeka.CLI.Application.Default;
using Jubeka.CLI.Infrastructure.Console;
using Jubeka.CLI.Infrastructure.Formatting;
using Jubeka.Core.Application.Default;
using Jubeka.Core.Infrastructure.IO;

namespace Jubeka.CLI;

public static class Startup
{
    public static Cli CreateCli()
    {
        HelpPrinter helpPrinter = new();
        ArgumentParser parser = new();

        QueryParser queryParser = new();
        HeaderParser headerParser = new();
        UriBuilderHelper uriBuilder = new(queryParser);
        BodyLoader bodyLoader = new();
        RequestDataBuilder requestBuilder = new(bodyLoader, headerParser, queryParser, uriBuilder);
        ResponseWriter responseWriter = new(new JsonResponseFormatter());

        return new Cli(helpPrinter, parser, requestBuilder, responseWriter);
    }
}
