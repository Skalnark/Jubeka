using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
                CliCommand.EnvRequestAdd => RunEnvRequestAdd((EnvRequestAddOptions)parseResult.Options),
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
            EnvironmentConfig? config = environmentConfigStore.Get(options.EnvName, Directory.GetCurrentDirectory());
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
        (EnvironmentConfig config, bool local) = BuildEnvironmentConfigInteractively(options, "create");
        environmentConfigStore.Save(config, local, Directory.GetCurrentDirectory());
        Console.WriteLine($"Environment '{config.Name}' created.");
        return 0;
    }

    private int RunEnvUpdate(EnvConfigOptions options)
    {
        EnvironmentConfig? existing = environmentConfigStore.Get(options.Name, Directory.GetCurrentDirectory());
        IReadOnlyList<RequestDefinition> requests = existing?.Requests ?? [];
        EnvironmentConfig config = new(options.Name, options.VarsPath, options.DefaultOpenApiSource, requests);
        environmentConfigStore.Save(config, options.Local, Directory.GetCurrentDirectory());
        Console.WriteLine($"Environment '{config.Name}' updated.");
        return 0;
    }

    private int RunEnvRequestAdd(EnvRequestAddOptions options)
    {
        EnvironmentConfig? config = environmentConfigStore.Get(options.EnvName, Directory.GetCurrentDirectory());
        if (config == null)
        {
            Console.Error.WriteLine($"Environment config not found: {options.EnvName}");
            return 1;
        }

        RequestDefinition request = BuildRequestDefinitionInteractively(options);
        List<RequestDefinition> requests = config.Requests?.ToList() ?? [];
        requests.Add(request);

        EnvironmentConfig updated = new(config.Name, config.VarsPath, config.DefaultOpenApiSource, requests);
        environmentConfigStore.Save(updated, options.Local, Directory.GetCurrentDirectory());
        Console.WriteLine($"Request '{request.Name}' added to '{config.Name}'.");
        return 0;
    }

    private static (EnvironmentConfig Config, bool Local) BuildEnvironmentConfigInteractively(EnvConfigOptions options, string action)
    {
        Console.WriteLine($"Starting env {action} wizard:");

        string name = PromptRequired("Name", options.Name);
        string varsDefault = string.IsNullOrWhiteSpace(options.VarsPath) ? $"{name}.yml" : options.VarsPath;
        string varsPath = PromptWithDefault("YAML vars path", varsDefault);

        OpenApiSource? source = options.DefaultOpenApiSource;
        bool? setSpec = PromptYesNo("Set default OpenAPI spec?", source != null);
        if (setSpec == true)
        {
            string kindInput = PromptRequired("Spec source (url|file|raw)", source?.Kind.ToString().ToLowerInvariant() ?? string.Empty);
            OpenApiSourceKind kind = kindInput switch
            {
                "url" => OpenApiSourceKind.Url,
                "file" => OpenApiSourceKind.File,
                "raw" => OpenApiSourceKind.Raw,
                _ => throw new OpenApiSpecificationException("Invalid spec source. Use url, file, or raw.")
            };

            string value = PromptRequired("Spec value", source?.Value ?? string.Empty);
            source = new OpenApiSource(kind, value);
        }

        bool local = PromptYesNo("Save locally (./.jubeka)", options.Local) ?? options.Local;
        return (new EnvironmentConfig(name, varsPath, source, []), local);
    }

    private static RequestDefinition BuildRequestDefinitionInteractively(EnvRequestAddOptions options)
    {
        Console.WriteLine("Starting request add wizard:");

        string name = PromptRequired("Request name", options.Name);
        string method = PromptRequired("Method", options.Method ?? "GET");
        string url = PromptRequired("URL", options.Url);
        string? body = PromptOptional("Body (optional)", options.Body);

        List<string> queries = options.QueryParams?.ToList() ?? [];
        while (PromptYesNo("Add query param?", null) == true)
        {
            string q = PromptRequired("Query (key=value)", null);
            queries.Add(q);
        }

        List<string> headers = options.Headers?.ToList() ?? [];
        while (PromptYesNo("Add header?", null) == true)
        {
            string h = PromptRequired("Header (Name: Value)", null);
            headers.Add(h);
        }

        return new RequestDefinition(name, method, url, body, queries, headers);
    }

    private static string PromptWithDefault(string label, string? defaultValue)
    {
        string prompt = string.IsNullOrWhiteSpace(defaultValue)
            ? $"{label}: "
            : $"{label} [{defaultValue}]: ";
        Console.Write(prompt);
        string? input = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(input))
        {
            return defaultValue ?? string.Empty;
        }

        return input.Trim();
    }

    private static string? PromptOptional(string label, string? defaultValue)
    {
        string prompt = string.IsNullOrWhiteSpace(defaultValue)
            ? $"{label}: "
            : $"{label} [{defaultValue}]: ";
        Console.Write(prompt);
        string? input = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(input))
        {
            return defaultValue;
        }

        return input.Trim();
    }

    private static string PromptRequired(string label, string? defaultValue)
    {
        while (true)
        {
            string prompt = string.IsNullOrWhiteSpace(defaultValue)
                ? $"{label}: "
                : $"{label} [{defaultValue}]: ";
            Console.Write(prompt);
            string? input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input))
            {
                if (!string.IsNullOrWhiteSpace(defaultValue))
                {
                    return defaultValue;
                }

                Console.WriteLine($"{label} is required.");
                continue;
            }

            return input.Trim();
        }
    }

    private static bool? PromptYesNo(string label, bool? defaultValue)
    {
        string suffix = defaultValue == true ? "[Y/n]" : defaultValue == false ? "[y/N]" : "[y/n]";
        Console.Write($"{label} {suffix}: ");
        string? input = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(input))
        {
            return defaultValue;
        }

        return input.Trim().StartsWith("y", StringComparison.OrdinalIgnoreCase);
    }
}
