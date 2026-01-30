using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Jubeka.CLI.Application;
using Jubeka.CLI.Domain;
using Jubeka.Core.Application;
using Jubeka.Core.Domain;
using Jubeka.Core.Infrastructure.Http;

namespace Jubeka.CLI;

public sealed class Cli(
    IHelpPrinter helpPrinter,
    IArgumentParser argumentParser,
    IRequestDataBuilder requestDataBuilder,
    IResponseWriter responseWriter,
    IEnvironmentVariablesLoader environmentVariablesLoader,
    IOpenApiSpecLoader openApiSpecLoader,
    IOpenApiRequestBuilder openApiRequestBuilder,
    IEnvironmentConfigStore environmentConfigStore)
{
    public async Task<int> RunAsync(string[] args, CancellationToken cancellationToken)
    {
        try
        {
            ParseResult parseResult = argumentParser.Parse(args);

            if (parseResult.ShowHelp || parseResult.Options is null || parseResult.Command is null)
            {
                return helpPrinter.Print(parseResult.Error);
            }

            return parseResult.Command switch
            {
                CliCommand.Request => await RunRequestAsync((RequestCommandOptions)parseResult.Options, cancellationToken).ConfigureAwait(false),
                CliCommand.OpenApiRequest => await RunOpenApiRequestAsync((OpenApiCommandOptions)parseResult.Options, cancellationToken).ConfigureAwait(false),
                CliCommand.EnvCreate => RunEnvCreate((EnvConfigOptions)parseResult.Options),
                CliCommand.EnvUpdate => RunEnvUpdate((EnvConfigOptions)parseResult.Options),
                _ => helpPrinter.Print("Unknown command.")
            };
        }
        catch (OpenApiSpecificationException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
        catch (MissingEnvironmentVariableException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
        catch (FileNotFoundException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private async Task<int> RunRequestAsync(RequestCommandOptions options, CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<string, string> vars = environmentVariablesLoader.Load(options.EnvPath);
        RequestOptions requestOptions = new(
            options.Method,
            options.Url,
            options.Body,
            options.QueryParams,
            options.Headers);

        RequestData requestData = requestDataBuilder.Build(requestOptions, vars);
        ResponseData response = await HttpRequestExecutor.ExecuteAsync(
            requestData,
            TimeSpan.FromSeconds(options.TimeoutSeconds),
            cancellationToken).ConfigureAwait(false);

        responseWriter.Write(response, options.Pretty);
        return response.IsSuccessStatusCode ? 0 : 1;
    }

    private async Task<int> RunOpenApiRequestAsync(OpenApiCommandOptions options, CancellationToken cancellationToken)
    {
        (OpenApiSource source, string envPath) = ResolveOpenApiInputs(options);
        IReadOnlyDictionary<string, string> vars = environmentVariablesLoader.Load(envPath);
        Microsoft.OpenApi.Models.OpenApiDocument document = await openApiSpecLoader.LoadAsync(source, cancellationToken).ConfigureAwait(false);

        RequestOptions openApiRequest = openApiRequestBuilder.Build(document, options.OperationId, vars);
        RequestData requestData = requestDataBuilder.Build(openApiRequest, vars);

        ResponseData response = await HttpRequestExecutor.ExecuteAsync(
            requestData,
            TimeSpan.FromSeconds(options.TimeoutSeconds),
            cancellationToken).ConfigureAwait(false);

        responseWriter.Write(response, options.Pretty);
        return response.IsSuccessStatusCode ? 0 : 1;
    }

    private (OpenApiSource Source, string EnvPath) ResolveOpenApiInputs(OpenApiCommandOptions options)
    {
        OpenApiSource? source = options.Source;
        string? envPath = options.EnvPath;

        if (!string.IsNullOrWhiteSpace(options.EnvName))
        {
            EnvironmentConfig? config = environmentConfigStore.Get(options.EnvName);
            if (config == null)
            {
                throw new OpenApiSpecificationException($"Environment config not found: {options.EnvName}");
            }

            source ??= config.DefaultOpenApiSource;
            envPath ??= config.VarsPath;
        }

        if (source == null)
        {
            throw new OpenApiSpecificationException("OpenAPI source is required. Use --spec-url, --spec-file, --spec-raw, or --env-name with a default spec.");
        }

        if (string.IsNullOrWhiteSpace(envPath))
        {
            throw new OpenApiSpecificationException("Environment YAML path is required. Use --env or --env-name.");
        }

        return (source, envPath);
    }

    private int RunEnvCreate(EnvConfigOptions options)
    {
        EnvironmentConfig config = new(options.Name, options.VarsPath, options.DefaultOpenApiSource);
        environmentConfigStore.Save(config);
        Console.WriteLine($"Environment '{options.Name}' created.");
        return 0;
    }

    private int RunEnvUpdate(EnvConfigOptions options)
    {
        EnvironmentConfig config = new(options.Name, options.VarsPath, options.DefaultOpenApiSource);
        environmentConfigStore.Save(config);
        Console.WriteLine($"Environment '{options.Name}' updated.");
        return 0;
    }
}
