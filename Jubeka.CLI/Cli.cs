using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jubeka.CLI.Application;
using Jubeka.CLI.Domain;
using Jubeka.Core.Application;
using Jubeka.Core.Domain;
using Jubeka.Core.Infrastructure.Http;

namespace Jubeka.CLI;

public sealed class Cli(IHelpPrinter helpPrinter,
    IArgumentParser argumentParser,
    IRequestDataBuilder requestDataBuilder,
    IResponseWriter responseWriter)
{
    public async Task<int> RunAsync(string[] args, CancellationToken cancellationToken)
    {
        try
        {
            ParseResult parseResult = argumentParser.Parse(args);

            if (parseResult.ShowHelp || parseResult.Options is null)
            {
                return helpPrinter.Print(parseResult.Error);
            }

            RequestOptions requestOptions = new(
                parseResult.Options.Method,
                parseResult.Options.Url,
                parseResult.Options.Body,
                parseResult.Options.QueryParams,
                parseResult.Options.Headers);

            RequestData requestData = requestDataBuilder.Build(requestOptions, new Dictionary<string, string>());

            ResponseData response = await HttpRequestExecutor.ExecuteAsync(
                requestData,
                TimeSpan.FromSeconds(parseResult.Options.TimeoutSeconds),
                cancellationToken).ConfigureAwait(false);

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
