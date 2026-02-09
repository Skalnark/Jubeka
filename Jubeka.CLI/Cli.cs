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
    IEnvironmentConfigStore environmentConfigStore,
    IEnvironmentWizard environmentWizard,
    IRequestWizard requestWizard)
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
                CliCommand.RequestAdd => RunEnvRequestAdd((EnvRequestAddOptions)parseResult.Options),
                CliCommand.RequestList => RunEnvRequestList((EnvRequestListOptions)parseResult.Options),
                CliCommand.RequestEdit => RunEnvRequestEdit((EnvRequestEditOptions)parseResult.Options),
                CliCommand.RequestExec => await RunEnvRequestExecAsync((EnvRequestExecOptions)parseResult.Options, cancellationToken).ConfigureAwait(false),
                CliCommand.OpenApiRequest => await RunOpenApiRequestAsync((OpenApiCommandOptions)parseResult.Options, cancellationToken).ConfigureAwait(false),
                CliCommand.EnvCreate => RunEnvCreate((EnvConfigOptions)parseResult.Options),
                CliCommand.EnvUpdate => RunEnvUpdate((EnvConfigOptions)parseResult.Options),
                CliCommand.EnvEdit => RunEnvEdit((EnvEditOptions)parseResult.Options),
                CliCommand.EnvSet => RunEnvSet((EnvSetOptions)parseResult.Options),
                CliCommand.EnvDelete => RunEnvDelete((EnvDeleteOptions)parseResult.Options),
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
        double timeoutSeconds = ResolveTimeoutSeconds(options.TimeoutSeconds, vars);
        RequestOptions requestOptions = new(
            options.Method,
            options.Url,
            options.Body,
            options.QueryParams,
            options.Headers);

        RequestData requestData = requestDataBuilder.Build(requestOptions, vars);
        ResponseData response = await HttpRequestExecutor.ExecuteAsync(
            requestData,
            TimeSpan.FromSeconds(timeoutSeconds),
            cancellationToken).ConfigureAwait(false);

        responseWriter.Write(response, options.Pretty);
        return response.IsSuccessStatusCode ? 0 : 1;
    }

    private async Task<int> RunOpenApiRequestAsync(OpenApiCommandOptions options, CancellationToken cancellationToken)
    {
        (OpenApiSource source, string envPath) = ResolveOpenApiInputs(options);
        IReadOnlyDictionary<string, string> vars = environmentVariablesLoader.Load(envPath);
        Microsoft.OpenApi.Models.OpenApiDocument document = await openApiSpecLoader.LoadAsync(source, cancellationToken).ConfigureAwait(false);

        double timeoutSeconds = ResolveTimeoutSeconds(options.TimeoutSeconds, vars);
        RequestOptions openApiRequest = openApiRequestBuilder.Build(document, options.OperationId, vars);
        RequestData requestData = requestDataBuilder.Build(openApiRequest, vars);

        ResponseData response = await HttpRequestExecutor.ExecuteAsync(
            requestData,
            TimeSpan.FromSeconds(timeoutSeconds),
            cancellationToken).ConfigureAwait(false);

        responseWriter.Write(response, options.Pretty);
        return response.IsSuccessStatusCode ? 0 : 1;
    }

    private (OpenApiSource Source, string EnvPath) ResolveOpenApiInputs(OpenApiCommandOptions options)
    {
        OpenApiSource? source = options.Source;
        string? envPath = options.EnvPath;

        string? envName = options.EnvName;
        if (string.IsNullOrWhiteSpace(envName))
        {
            envName = ResolveCurrentEnv(null);
        }

        if (!string.IsNullOrWhiteSpace(envName))
        {
            EnvironmentConfig? config = environmentConfigStore.Get(envName);
            if (config == null)
            {
                throw new OpenApiSpecificationException($"Environment config not found: {envName}");
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

    private int RunEnvSet(EnvSetOptions options)
    {
        EnvironmentConfig? config = environmentConfigStore.Get(options.Name);
        if (config == null)
        {
            Console.Error.WriteLine($"Environment config not found: {options.Name}");
            return 1;
        }

        environmentConfigStore.SetCurrent(options.Name);
        Console.WriteLine($"Current environment set to '{options.Name}'.");
        return 0;
    }

    private int RunEnvDelete(EnvDeleteOptions options)
    {
        string? envName = ResolveCurrentEnv(options.Name);
        if (string.IsNullOrWhiteSpace(envName))
        {
            Console.Error.WriteLine("No environment selected. Use --name or set a current environment.");
            return 1;
        }

        if (!environmentConfigStore.Delete(envName))
        {
            Console.Error.WriteLine($"Environment config not found: {envName}");
            return 1;
        }

        Console.WriteLine($"Environment '{envName}' deleted.");
        return 0;
    }

    private string? ResolveCurrentEnv(string? explicitName)
    {
        if (!string.IsNullOrWhiteSpace(explicitName))
        {
            return explicitName;
        }

        return environmentConfigStore.GetCurrent();
    }

    private int RunEnvCreate(EnvConfigOptions options)
    {
        EnvironmentConfig config;
        if (HasEnvCreateInput(options))
        {
            if (string.IsNullOrWhiteSpace(options.Name))
            {
                Console.Error.WriteLine("--name is required when providing env create options.");
                return 1;
            }

            string varsPath = string.IsNullOrWhiteSpace(options.VarsPath)
                ? $"{options.Name}.yml"
                : options.VarsPath;
            config = new EnvironmentConfig(options.Name, varsPath, options.DefaultOpenApiSource, []);
        }
        else
        {
            config = environmentWizard.BuildEnvironmentConfig(options, "create");
        }

        environmentConfigStore.Save(config);
        Console.WriteLine($"Environment '{config.Name}' created.");
        return 0;
    }

    private int RunEnvUpdate(EnvConfigOptions options)
    {
        EnvironmentConfig? existing = environmentConfigStore.Get(options.Name);
        IReadOnlyList<RequestDefinition> requests = existing?.Requests ?? [];
        EnvironmentConfig config = new(options.Name, options.VarsPath, options.DefaultOpenApiSource, requests);
        environmentConfigStore.Save(config);
        Console.WriteLine($"Environment '{config.Name}' updated.");
        return 0;
    }

    private int RunEnvEdit(EnvEditOptions options)
    {
        EnvironmentConfig? existing = environmentConfigStore.Get(options.Name);
        if (existing == null)
        {
            Console.Error.WriteLine($"Environment config not found: {options.Name}");
            return 1;
        }

        if (options.Inline)
        {
            string varsPath = string.IsNullOrWhiteSpace(options.VarsPath) ? existing.VarsPath : options.VarsPath;
            OpenApiSource? source = options.DefaultOpenApiSource ?? existing.DefaultOpenApiSource;
            EnvironmentConfig updated = new(options.Name, varsPath, source, existing.Requests);
            environmentConfigStore.Save(updated);
            Console.WriteLine($"Environment '{updated.Name}' updated.");
            return 0;
        }

        EnvConfigOptions wizardOptions = new(options.Name, existing.VarsPath, existing.DefaultOpenApiSource);
        EnvironmentConfig wizardConfig = environmentWizard.BuildEnvironmentConfig(wizardOptions, "edit");
        EnvironmentConfig result = new(wizardConfig.Name, wizardConfig.VarsPath, wizardConfig.DefaultOpenApiSource, existing.Requests);
        environmentConfigStore.Save(result);
        Console.WriteLine($"Environment '{result.Name}' updated.");
        return 0;
    }

    private int RunEnvRequestAdd(EnvRequestAddOptions options)
    {
        string? envName = ResolveCurrentEnv(options.EnvName);
        if (string.IsNullOrWhiteSpace(envName))
        {
            Console.Error.WriteLine("No environment selected. Use --name or set a current environment.");
            return 1;
        }

        EnvironmentConfig? config = environmentConfigStore.Get(envName);
        if (config == null)
        {
            Console.Error.WriteLine($"Environment config not found: {envName}");
            return 1;
        }

        IReadOnlyDictionary<string, string> vars = LoadEnvVarsSafe(config.VarsPath);
        RequestDefinition request;
        if (HasRequestAddInput(options))
        {
            if (string.IsNullOrWhiteSpace(options.Name) || string.IsNullOrWhiteSpace(options.Url))
            {
                Console.Error.WriteLine("--req-name and --url are required when providing request options.");
                return 1;
            }

            string method = string.IsNullOrWhiteSpace(options.Method) ? "GET" : options.Method;
            List<QueryParamDefinition> queries = NormalizeQueryParams(options.QueryParams);
            List<string> headers = options.Headers?.ToList() ?? [];
            request = new RequestDefinition(options.Name, method, options.Url, options.Body, queries, headers, new AuthConfig(AuthMethod.Inherit));
        }
        else
        {
            request = requestWizard.BuildRequest(options, vars);
        }
        List<RequestDefinition> requests = config.Requests?.ToList() ?? [];
        requests.Add(request);

        EnvironmentConfig updated = new(config.Name, config.VarsPath, config.DefaultOpenApiSource, requests);
        environmentConfigStore.Save(updated);
        Console.WriteLine($"Request '{request.Name}' added to '{config.Name}'.");
        return 0;
    }

    private int RunEnvRequestList(EnvRequestListOptions options)
    {
        string? envName = ResolveCurrentEnv(options.EnvName);
        if (string.IsNullOrWhiteSpace(envName))
        {
            Console.Error.WriteLine("No environment selected. Use --name or set a current environment.");
            return 1;
        }

        EnvironmentConfig? config = environmentConfigStore.Get(envName);
        if (config == null)
        {
            Console.Error.WriteLine($"Environment config not found: {envName}");
            return 1;
        }

        if (config.Requests.Count == 0)
        {
            Console.WriteLine("No requests in this environment.");
            return 0;
        }

        for (int i = 0; i < config.Requests.Count; i++)
        {
            RequestDefinition request = config.Requests[i];
            Console.WriteLine($"{i + 1}. {request.Name} [{request.Method}] {request.Url}");
        }

        return 0;
    }

    private int RunEnvRequestEdit(EnvRequestEditOptions options)
    {
        string? envName = ResolveCurrentEnv(options.EnvName);
        if (string.IsNullOrWhiteSpace(envName))
        {
            Console.Error.WriteLine("No environment selected. Use --name or set a current environment.");
            return 1;
        }

        EnvironmentConfig? config = environmentConfigStore.Get(envName);
        if (config == null)
        {
            Console.Error.WriteLine($"Environment config not found: {envName}");
            return 1;
        }

        if (config.Requests.Count == 0)
        {
            Console.WriteLine("No requests in this environment.");
            return 0;
        }

        if (options.Inline)
        {
            if (string.IsNullOrWhiteSpace(options.RequestName))
            {
                Console.Error.WriteLine("--req-name is required when using --inline.");
                return 1;
            }

            int inlineIndex = FindRequestIndexByName(config.Requests, options.RequestName);
            if (inlineIndex < 0)
            {
                Console.Error.WriteLine($"Request not found: {options.RequestName}");
                return 1;
            }

            RequestDefinition updatedRequest = ApplyInlineEdits(config.Requests[inlineIndex], options);
            List<RequestDefinition> inlineRequests = [.. config.Requests];
            inlineRequests[inlineIndex] = updatedRequest;
            EnvironmentConfig inlineConfig = new(config.Name, config.VarsPath, config.DefaultOpenApiSource, inlineRequests);
            environmentConfigStore.Save(inlineConfig);
            Console.WriteLine($"Request '{updatedRequest.Name}' updated in '{config.Name}'.");
            return 0;
        }

        IReadOnlyDictionary<string, string> vars = LoadEnvVarsSafe(config.VarsPath);
        int index = requestWizard.SelectRequestIndex(config.Requests, options.RequestName);
        if (index < 0)
        {
            Console.WriteLine("No request selected.");
            return 0;
        }

        RequestDefinition edited = requestWizard.EditRequest(config.Requests[index], vars);
        List<RequestDefinition> updatedRequests = [.. config.Requests];
        updatedRequests[index] = edited;

        EnvironmentConfig updated = new(config.Name, config.VarsPath, config.DefaultOpenApiSource, updatedRequests);
        environmentConfigStore.Save(updated);
        Console.WriteLine($"Request '{edited.Name}' updated in '{config.Name}'.");
        return 0;
    }

    private async Task<int> RunEnvRequestExecAsync(EnvRequestExecOptions options, CancellationToken cancellationToken)
    {
        string? envName = ResolveCurrentEnv(options.EnvName);
        if (string.IsNullOrWhiteSpace(envName))
        {
            Console.Error.WriteLine("No environment selected. Use --name or set a current environment.");
            return 1;
        }

        if (string.IsNullOrWhiteSpace(options.RequestName))
        {
            Console.Error.WriteLine("--req-name is required.");
            return 1;
        }

        EnvironmentConfig? config = environmentConfigStore.Get(envName);
        if (config == null)
        {
            Console.Error.WriteLine($"Environment config not found: {envName}");
            return 1;
        }

        int index = FindRequestIndexByName(config.Requests, options.RequestName);
        if (index < 0)
        {
            Console.Error.WriteLine($"Request not found: {options.RequestName}");
            return 1;
        }

        IReadOnlyDictionary<string, string> vars = LoadEnvVarsSafe(config.VarsPath);
        double timeoutSeconds = ResolveTimeoutSeconds(options.TimeoutSeconds, vars);
        RequestDefinition request = config.Requests[index];
        RequestOptions requestOptions = new(
            request.Method,
            request.Url,
            request.Body,
            request.QueryParams.Select(q => $"{q.Key}={q.Value}").ToList(),
            request.Headers);

        RequestData requestData = requestDataBuilder.Build(requestOptions, vars);
        ResponseData response = await HttpRequestExecutor.ExecuteAsync(
            requestData,
            TimeSpan.FromSeconds(timeoutSeconds),
            cancellationToken).ConfigureAwait(false);

        responseWriter.Write(response, false);
        return response.IsSuccessStatusCode ? 0 : 1;
    }

    private static int FindRequestIndexByName(IReadOnlyList<RequestDefinition> requests, string name)
    {
        for (int i = 0; i < requests.Count; i++)
        {
            if (string.Equals(requests[i].Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    private static RequestDefinition ApplyInlineEdits(RequestDefinition request, EnvRequestEditOptions options)
    {
        string method = string.IsNullOrWhiteSpace(options.Method) ? request.Method : options.Method;
        string url = string.IsNullOrWhiteSpace(options.Url) ? request.Url : options.Url;
        string? body = options.Body ?? request.Body;
        IReadOnlyList<QueryParamDefinition> queries = options.QueryParams?.Count > 0
            ? NormalizeQueryParams(options.QueryParams)
            : request.QueryParams;
        IReadOnlyList<string> headers = options.Headers?.Count > 0
            ? options.Headers.ToList()
            : request.Headers;
        return new RequestDefinition(request.Name, method, url, body, queries, headers, request.Auth);
    }

    private static bool HasEnvCreateInput(EnvConfigOptions options)
    {
        return !string.IsNullOrWhiteSpace(options.Name)
               || !string.IsNullOrWhiteSpace(options.VarsPath)
               || options.DefaultOpenApiSource != null;
    }

    private static bool HasRequestAddInput(EnvRequestAddOptions options)
    {
        return !string.IsNullOrWhiteSpace(options.Name)
               || !string.IsNullOrWhiteSpace(options.Method)
               || !string.IsNullOrWhiteSpace(options.Url)
               || !string.IsNullOrWhiteSpace(options.Body)
               || (options.QueryParams?.Count ?? 0) > 0
               || (options.Headers?.Count ?? 0) > 0;
    }

    private static List<QueryParamDefinition> NormalizeQueryParams(IReadOnlyList<string>? raw)
    {
        List<QueryParamDefinition> results = [];
        if (raw == null)
        {
            return results;
        }

        foreach (string entry in raw)
        {
            int idx = entry.IndexOf('=');
            if (idx <= 0)
            {
                continue;
            }

            string key = entry.Substring(0, idx);
            string value = entry.Substring(idx + 1);
            results.Add(new QueryParamDefinition(key, value));
        }

        return results;
    }

    private static double ResolveTimeoutSeconds(double? requestedSeconds, IReadOnlyDictionary<string, string> vars)
    {
        if (requestedSeconds.HasValue)
        {
            return requestedSeconds.Value;
        }

        if (vars.TryGetValue(OpenApiTimeoutSecondsKey, out string? raw)
            && double.TryParse(raw, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double parsed)
            && parsed > 0)
        {
            return parsed;
        }

        return DefaultTimeoutSeconds;
    }

    private IReadOnlyDictionary<string, string> LoadEnvVarsSafe(string? path)
    {
        try
        {
            return environmentVariablesLoader.Load(path);
        }
        catch (Exception ex)
        {
            if (!string.IsNullOrWhiteSpace(path))
            {
                Console.Error.WriteLine($"Failed to load env vars from '{path}': {ex.Message}");
            }
            return new Dictionary<string, string>();
        }
    }

    private const double DefaultTimeoutSeconds = 100;
    private const string OpenApiTimeoutSecondsKey = "openApiTimeoutSeconds";
}
