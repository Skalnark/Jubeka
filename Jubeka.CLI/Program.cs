using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jubeka.CLI.Application.Default;
using Jubeka.CLI.Domain;
using Jubeka.CLI.Infrastructure.Formatting;
using Jubeka.CLI.Infrastructure.Console;
using Jubeka.Core.Application.Default;
using Jubeka.Core.Domain;
using Jubeka.Core.Infrastructure.Http;
using Jubeka.Core.Infrastructure.IO;

namespace Jubeka.CLI;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            HelpPrinter helpPrinter = new();
            ArgumentParser parser = new();
            ParseResult parseResult = parser.Parse(args);

            if (parseResult.ShowHelp || parseResult.Options is null)
            {
                return helpPrinter.Print(parseResult.Error);
            }

            QueryParser queryParser = new();
            HeaderParser headerParser = new();
            UriBuilderHelper uriBuilder = new(queryParser);
            BodyLoader bodyLoader = new();
            RequestDataBuilder requestBuilder = new(bodyLoader, headerParser, queryParser, uriBuilder);
            ResponseWriter responseWriter = new(new JsonResponseFormatter());

            RequestOptions requestOptions = new(
                parseResult.Options.Method,
                parseResult.Options.Url,
                parseResult.Options.Body,
                parseResult.Options.QueryParams,
                parseResult.Options.Headers);


            RequestData requestData = requestBuilder.Build(requestOptions, new Dictionary<string, string>());// TODO: implement vars from env
            ResponseData response = await HttpRequestExecutor.ExecuteAsync(requestData, TimeSpan.FromSeconds(parseResult.Options.TimeoutSeconds), CancellationToken.None);

            responseWriter.Write(response, parseResult.Options.Pretty);

            return response.IsSuccessStatusCode ? 0 : 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }
}