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
                CliCommand.EnvRequestList => RunEnvRequestList((EnvRequestListOptions)parseResult.Options),
                CliCommand.EnvRequestEdit => RunEnvRequestEdit((EnvRequestEditOptions)parseResult.Options),
                CliCommand.EnvSet => RunEnvSet((EnvSetOptions)parseResult.Options),
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

        string? envName = options.EnvName;
        if (string.IsNullOrWhiteSpace(envName))
        {
            (envName, _) = ResolveCurrentEnv(null, false);
        }

        if (!string.IsNullOrWhiteSpace(envName))
        {
            EnvironmentConfig? config = environmentConfigStore.Get(envName, Directory.GetCurrentDirectory());
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
        EnvironmentConfig? config = environmentConfigStore.Get(options.Name, Directory.GetCurrentDirectory());
        if (config == null)
        {
            Console.Error.WriteLine($"Environment config not found: {options.Name}");
            return 1;
        }

        environmentConfigStore.SetCurrent(options.Name, options.Local, Directory.GetCurrentDirectory());
        Console.WriteLine($"Current environment set to '{options.Name}'.");
        return 0;
    }

    private (string? Name, bool Local) ResolveCurrentEnv(string? explicitName, bool localFlag)
    {
        if (!string.IsNullOrWhiteSpace(explicitName))
        {
            return (explicitName, localFlag);
        }

        return environmentConfigStore.GetCurrentInfo(Directory.GetCurrentDirectory());
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
        (string? envName, bool scopeLocal) = ResolveCurrentEnv(options.EnvName, options.Local);
        if (string.IsNullOrWhiteSpace(envName))
        {
            Console.Error.WriteLine("No environment selected. Use --name or set a current environment.");
            return 1;
        }

        EnvironmentConfig? config = environmentConfigStore.Get(envName, Directory.GetCurrentDirectory());
        if (config == null)
        {
            Console.Error.WriteLine($"Environment config not found: {envName}");
            return 1;
        }

        IReadOnlyDictionary<string, string> vars = LoadEnvVarsSafe(config.VarsPath);
        RequestDefinition request = BuildRequestDefinitionInteractively(options, vars);
        List<RequestDefinition> requests = config.Requests?.ToList() ?? [];
        requests.Add(request);

        EnvironmentConfig updated = new(config.Name, config.VarsPath, config.DefaultOpenApiSource, requests);
        environmentConfigStore.Save(updated, scopeLocal, Directory.GetCurrentDirectory());
        Console.WriteLine($"Request '{request.Name}' added to '{config.Name}'.");
        return 0;
    }

    private int RunEnvRequestList(EnvRequestListOptions options)
    {
        (string? envName, _) = ResolveCurrentEnv(options.EnvName, options.Local);
        if (string.IsNullOrWhiteSpace(envName))
        {
            Console.Error.WriteLine("No environment selected. Use --name or set a current environment.");
            return 1;
        }

        EnvironmentConfig? config = environmentConfigStore.Get(envName, Directory.GetCurrentDirectory());
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
        (string? envName, bool scopeLocal) = ResolveCurrentEnv(options.EnvName, options.Local);
        if (string.IsNullOrWhiteSpace(envName))
        {
            Console.Error.WriteLine("No environment selected. Use --name or set a current environment.");
            return 1;
        }

        EnvironmentConfig? config = environmentConfigStore.Get(envName, Directory.GetCurrentDirectory());
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

        IReadOnlyDictionary<string, string> vars = LoadEnvVarsSafe(config.VarsPath);
        int index = SelectRequestIndex(config.Requests, options.RequestName);
        if (index < 0)
        {
            Console.WriteLine("No request selected.");
            return 0;
        }

        RequestDefinition edited = EditRequestInteractively(config.Requests[index], vars);
        List<RequestDefinition> updatedRequests = config.Requests.ToList();
        updatedRequests[index] = edited;

        EnvironmentConfig updated = new(config.Name, config.VarsPath, config.DefaultOpenApiSource, updatedRequests);
        environmentConfigStore.Save(updated, scopeLocal, Directory.GetCurrentDirectory());
        Console.WriteLine($"Request '{edited.Name}' updated in '{config.Name}'.");
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

    private static RequestDefinition BuildRequestDefinitionInteractively(EnvRequestAddOptions options, IReadOnlyDictionary<string, string> vars)
    {
        Console.WriteLine("Starting request add wizard:");

        string name = PromptRequired("Request name", options.Name);
        string method = PromptRequired("Method", options.Method ?? "GET");
        string url = PromptRequired("URL", options.Url);
        string? body = PromptOptional("Body (optional)", options.Body);

        List<QueryParamDefinition> queries = NormalizeQueryParams(options.QueryParams);
        ManageQueryParams(queries, vars);

        List<string> headers = options.Headers?.ToList() ?? [];
        ManageHeaders(headers);

        AuthConfig auth = BuildAuthConfigInteractively(vars);

        return new RequestDefinition(name, method, url, body, queries, headers, auth);
    }

    private static RequestDefinition EditRequestInteractively(RequestDefinition request, IReadOnlyDictionary<string, string> vars)
    {
        string name = request.Name;
        string method = request.Method;
        string url = request.Url;
        string? body = request.Body;
        List<QueryParamDefinition> queries = request.QueryParams.ToList();
        List<string> headers = request.Headers.ToList();
        AuthConfig auth = request.Auth;

        while (true)
        {
            Console.WriteLine();
            Console.WriteLine("Edit request:");
            Console.WriteLine("1) Name");
            Console.WriteLine("2) Method");
            Console.WriteLine("3) URL");
            Console.WriteLine("4) Body");
            Console.WriteLine("5) Query params");
            Console.WriteLine("6) Headers");
            Console.WriteLine("7) Auth");
            Console.WriteLine("8) Save");
            Console.WriteLine("9) Cancel");

            string choice = PromptWithDefault("Select", "8");
            switch (choice)
            {
                case "1":
                    name = PromptRequired("Name", name);
                    break;
                case "2":
                    method = PromptRequired("Method", method);
                    break;
                case "3":
                    url = PromptRequired("URL", url);
                    break;
                case "4":
                    body = PromptOptional("Body (optional)", body);
                    break;
                case "5":
                    ManageQueryParams(queries, vars);
                    break;
                case "6":
                    ManageHeaders(headers);
                    break;
                case "7":
                    auth = BuildAuthConfigInteractively(vars, auth);
                    break;
                case "8":
                    return new RequestDefinition(name, method, url, body, queries, headers, auth);
                case "9":
                    return request;
                default:
                    Console.WriteLine("Invalid option.");
                    break;
            }
        }
    }

    private int SelectRequestIndex(IReadOnlyList<RequestDefinition> requests, string? requestName)
    {
        if (!string.IsNullOrWhiteSpace(requestName))
        {
            int idx = requests.ToList().FindIndex(r => string.Equals(r.Name, requestName, StringComparison.OrdinalIgnoreCase));
            return idx;
        }

        Console.WriteLine("Select a request:");
        for (int i = 0; i < requests.Count; i++)
        {
            Console.WriteLine($"{i + 1}. {requests[i].Name} [{requests[i].Method}] {requests[i].Url}");
        }

        string input = PromptWithDefault("Enter number (blank to cancel)", string.Empty);
        if (string.IsNullOrWhiteSpace(input))
        {
            return -1;
        }

        if (int.TryParse(input, out int choice) && choice >= 1 && choice <= requests.Count)
        {
            return choice - 1;
        }

        Console.WriteLine("Invalid selection.");
        return -1;
    }

    private static void ManageQueryParams(List<QueryParamDefinition> queries, IReadOnlyDictionary<string, string> vars)
    {
        while (true)
        {
            Console.WriteLine();
            Console.WriteLine("Query params:");
            if (queries.Count == 0)
            {
                Console.WriteLine("(none)");
            }
            else
            {
                for (int i = 0; i < queries.Count; i++)
                {
                    Console.WriteLine($"{i + 1}. {queries[i].Key}={queries[i].Value}");
                }
            }

            Console.WriteLine("1) Add new");
            Console.WriteLine("2) Change");
            Console.WriteLine("3) Delete");
            Console.WriteLine("4) Select existing variable");
            Console.WriteLine("5) Done");

            string choice = PromptWithDefault("Select", "5");
            switch (choice)
            {
                case "1":
                    AddQueryParam(queries, vars, allowVarSelection: true);
                    break;
                case "2":
                    ChangeQueryParam(queries, vars);
                    break;
                case "3":
                    DeleteQueryParam(queries);
                    break;
                case "4":
                    AddQueryParam(queries, vars, allowVarSelection: true, forceSelectVar: true);
                    break;
                case "5":
                    return;
                default:
                    Console.WriteLine("Invalid option.");
                    break;
            }
        }
    }

    private static void AddQueryParam(List<QueryParamDefinition> queries, IReadOnlyDictionary<string, string> vars, bool allowVarSelection, bool forceSelectVar = false)
    {
        string key = PromptRequired("Query key", null);
        string value;

        if (allowVarSelection && vars.Count > 0 && (forceSelectVar || PromptYesNo("Use existing variable?", false) == true))
        {
            string selected = PromptSelectVariable(vars);
            value = $"{{{{{selected}}}}}";
        }
        else
        {
            value = PromptRequired("Query value", null);
        }

        queries.Add(new QueryParamDefinition(key, value));
    }

    private static void ChangeQueryParam(List<QueryParamDefinition> queries, IReadOnlyDictionary<string, string> vars)
    {
        if (queries.Count == 0)
        {
            Console.WriteLine("No query params to change.");
            return;
        }

        int index = PromptIndex("Select query param", queries.Count);
        if (index < 0)
        {
            return;
        }

        QueryParamDefinition current = queries[index];
        string key = PromptRequired("Query key", current.Key);
        string value;
        if (vars.Count > 0 && PromptYesNo("Use existing variable?", false) == true)
        {
            string selected = PromptSelectVariable(vars);
            value = $"{{{{{selected}}}}}";
        }
        else
        {
            value = PromptRequired("Query value", current.Value);
        }

        queries[index] = new QueryParamDefinition(key, value);
    }

    private static void DeleteQueryParam(List<QueryParamDefinition> queries)
    {
        if (queries.Count == 0)
        {
            Console.WriteLine("No query params to delete.");
            return;
        }

        int index = PromptIndex("Select query param to delete", queries.Count);
        if (index < 0)
        {
            return;
        }

        queries.RemoveAt(index);
    }

    private static void ManageHeaders(List<string> headers)
    {
        while (true)
        {
            Console.WriteLine();
            Console.WriteLine("Headers:");
            if (headers.Count == 0)
            {
                Console.WriteLine("(none)");
            }
            else
            {
                for (int i = 0; i < headers.Count; i++)
                {
                    Console.WriteLine($"{i + 1}. {headers[i]}");
                }
            }

            Console.WriteLine("1) Add new");
            Console.WriteLine("2) Change");
            Console.WriteLine("3) Delete");
            Console.WriteLine("4) Done");

            string choice = PromptWithDefault("Select", "4");
            switch (choice)
            {
                case "1":
                    headers.Add(PromptRequired("Header (Name: Value)", null));
                    break;
                case "2":
                    if (headers.Count == 0)
                    {
                        Console.WriteLine("No headers to change.");
                        break;
                    }
                    int index = PromptIndex("Select header", headers.Count);
                    if (index >= 0)
                    {
                        headers[index] = PromptRequired("Header (Name: Value)", headers[index]);
                    }
                    break;
                case "3":
                    if (headers.Count == 0)
                    {
                        Console.WriteLine("No headers to delete.");
                        break;
                    }
                    int deleteIndex = PromptIndex("Select header to delete", headers.Count);
                    if (deleteIndex >= 0)
                    {
                        headers.RemoveAt(deleteIndex);
                    }
                    break;
                case "4":
                    return;
                default:
                    Console.WriteLine("Invalid option.");
                    break;
            }
        }
    }

    private static AuthConfig BuildAuthConfigInteractively(IReadOnlyDictionary<string, string> vars, AuthConfig? current = null)
    {
        AuthMethod method = current?.Method ?? AuthMethod.Inherit;
        Console.WriteLine();
        Console.WriteLine("Auth method:");
        Console.WriteLine("1) Inherit");
        Console.WriteLine("2) None");
        Console.WriteLine("3) Basic");
        Console.WriteLine("4) Bearer");

        string choice = PromptWithDefault("Select", ((int)method + 1).ToString());
        method = choice switch
        {
            "1" => AuthMethod.Inherit,
            "2" => AuthMethod.None,
            "3" => AuthMethod.Basic,
            "4" => AuthMethod.Bearer,
            _ => method
        };

        return method switch
        {
            AuthMethod.Basic => new AuthConfig(method,
                Username: PromptValueOrVariable("Username", vars, current?.Username),
                Password: PromptValueOrVariable("Password", vars, current?.Password)),
            AuthMethod.Bearer => new AuthConfig(method,
                Token: PromptValueOrVariable("Token", vars, current?.Token)),
            _ => new AuthConfig(method)
        };
    }

    private static string PromptValueOrVariable(string label, IReadOnlyDictionary<string, string> vars, string? defaultValue)
    {
        if (vars.Count > 0 && PromptYesNo($"Use existing variable for {label}?", false) == true)
        {
            string selected = PromptSelectVariable(vars);
            return $"{{{{{selected}}}}}";
        }

        return PromptRequired(label, defaultValue);
    }

    private static string PromptSelectVariable(IReadOnlyDictionary<string, string> vars)
    {
        List<string> keys = vars.Keys.OrderBy(k => k).ToList();
        for (int i = 0; i < keys.Count; i++)
        {
            Console.WriteLine($"{i + 1}. {keys[i]}");
        }

        int index = PromptIndex("Select variable", keys.Count);
        if (index < 0)
        {
            throw new OpenApiSpecificationException("No variable selected.");
        }

        return keys[index];
    }

    private static int PromptIndex(string label, int max)
    {
        string input = PromptWithDefault($"{label} (1-{max}, blank to cancel)", string.Empty);
        if (string.IsNullOrWhiteSpace(input))
        {
            return -1;
        }

        return int.TryParse(input, out int index) && index >= 1 && index <= max ? index - 1 : -1;
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

    private IReadOnlyDictionary<string, string> LoadEnvVarsSafe(string? path)
    {
        try
        {
            return environmentVariablesLoader.Load(path);
        }
        catch
        {
            return new Dictionary<string, string>();
        }
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
