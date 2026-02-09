using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Jubeka.CLI.Application;
using Jubeka.CLI.Domain;
using Jubeka.Core.Domain;
using Jubeka.Core.Infrastructure.OpenApi;
using Microsoft.OpenApi.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Jubeka.CLI.Infrastructure.Config;

public sealed partial class EnvironmentConfigStore : IEnvironmentConfigStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private static readonly ISerializer YamlSerializer = new SerializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    private static readonly IDeserializer YamlDeserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public EnvironmentConfig? Get(string name)
    {
        string globalDir = GetGlobalEnvDirectory(name);
        return LoadFromDirectory(globalDir);
    }

    public void Save(EnvironmentConfig config)
    {
        string envDirectory = GetGlobalEnvDirectory(config.Name);
        Directory.CreateDirectory(envDirectory);

        string varsPath = WriteVarsFile(envDirectory, config.VarsPath);
        OpenApiSource? openApiSource = WriteOpenApiSource(envDirectory, config.DefaultOpenApiSource);
        OpenApiDocument? openApiDocument = LoadOpenApiDocument(openApiSource);
        UpdateVarsFileWithOpenApi(varsPath, openApiDocument);
        List<RequestDefinition> mergedRequests = MergeRequestsWithOpenApi(config.Requests, openApiDocument);
        List<string> requestFiles = WriteRequestFiles(envDirectory, mergedRequests);

        EnvironmentConfigPersisted persisted = new(
            Name: config.Name,
            VarsFile: Path.GetFileName(varsPath),
            OpenApiKind: openApiSource?.Kind,
            OpenApiFile: openApiSource != null ? Path.GetFileName(openApiSource.Value ?? string.Empty) : null,
            RequestFiles: requestFiles);

        string configPath = GetConfigPath(envDirectory);
        string json = JsonSerializer.Serialize(persisted, SerializerOptions);
        File.WriteAllText(configPath, json);
    }

    public bool Delete(string name)
    {
        string envDirectory = GetGlobalEnvDirectory(name);
        if (!Directory.Exists(envDirectory))
        {
            return false;
        }

        Directory.Delete(envDirectory, true);

        string? current = GetCurrent();
        if (string.Equals(current, name, StringComparison.OrdinalIgnoreCase))
        {
            string currentPath = GetGlobalCurrentPath();
            if (File.Exists(currentPath))
            {
                File.Delete(currentPath);
            }
        }

        return true;
    }

    public string? GetCurrent()
    {
        string globalPath = GetGlobalCurrentPath();
        if (!File.Exists(globalPath))
        {
            return null;
        }

        string globalJson = File.ReadAllText(globalPath);
        CurrentEnvironment? current = JsonSerializer.Deserialize<CurrentEnvironment>(globalJson, SerializerOptions);
        return current?.Name;
    }

    public void SetCurrent(string name)
    {
        string directory = GetGlobalConfigDirectory();
        Directory.CreateDirectory(directory);

        string path = GetGlobalCurrentPath();
        string json = JsonSerializer.Serialize(new CurrentEnvironment(name), SerializerOptions);
        File.WriteAllText(path, json);
    }

    private static EnvironmentConfig? LoadFromDirectory(string envDirectory)
    {
        string configPath = GetConfigPath(envDirectory);
        if (!File.Exists(configPath))
        {
            return null;
        }

        string json = File.ReadAllText(configPath);
        EnvironmentConfigPersisted? persisted = JsonSerializer.Deserialize<EnvironmentConfigPersisted>(json, SerializerOptions);
        if (persisted == null)
        {
            return null;
        }

        string varsPath = Path.Combine(envDirectory, string.IsNullOrWhiteSpace(persisted.VarsFile) ? VarsFileName : persisted.VarsFile);
        OpenApiSource? source = ReadOpenApiSource(envDirectory, persisted);
        List<RequestDefinition> requests = ReadRequestFiles(envDirectory, persisted.RequestFiles);
        return new EnvironmentConfig(persisted.Name, varsPath, source, requests);
    }

    private static string WriteVarsFile(string envDirectory, string varsPath)
    {
        string targetPath = Path.Combine(envDirectory, VarsFileName);
        if (!string.IsNullOrWhiteSpace(varsPath) && File.Exists(varsPath))
        {
            string sourceFullPath = Path.GetFullPath(varsPath);
            string targetFullPath = Path.GetFullPath(targetPath);
            if (!string.Equals(sourceFullPath, targetFullPath, StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(varsPath, targetPath, true);
            }
        }
        else if (!File.Exists(targetPath))
        {
            File.WriteAllText(targetPath, "variables:\n");
        }

        return targetPath;
    }

    private static OpenApiSource? WriteOpenApiSource(string envDirectory, OpenApiSource? source)
    {
        if (source == null)
        {
            return null;
        }

        string content = source.Kind switch
        {
            OpenApiSourceKind.Url => DownloadOpenApiSource(source.Value ?? string.Empty),
            OpenApiSourceKind.Raw => source.Value ?? string.Empty,
            OpenApiSourceKind.File => ReadOpenApiFile(source.Value),
            _ => source.Value ?? string.Empty
        };

        string fileName = GetOpenApiFileName(content, source);
        string path = Path.Combine(envDirectory, fileName);

        File.WriteAllText(path, content ?? string.Empty);
        return new OpenApiSource(OpenApiSourceKind.File, path);
    }

    private static OpenApiSource? ReadOpenApiSource(string envDirectory, EnvironmentConfigPersisted persisted)
    {
        if (persisted.OpenApiKind == null || string.IsNullOrWhiteSpace(persisted.OpenApiFile))
        {
            return null;
        }

        string path = Path.Combine(envDirectory, persisted.OpenApiFile);
        if (!File.Exists(path))
        {
            return null;
        }

        string content = File.ReadAllText(path);
        return persisted.OpenApiKind.Value switch
        {
            OpenApiSourceKind.Url => new OpenApiSource(OpenApiSourceKind.Url, content.Trim()),
            OpenApiSourceKind.Raw => new OpenApiSource(OpenApiSourceKind.Raw, content),
            OpenApiSourceKind.File => new OpenApiSource(OpenApiSourceKind.File, path),
            _ => null
        };
    }

    private static List<RequestDefinition> MergeRequestsWithOpenApi(IReadOnlyList<RequestDefinition> requests, OpenApiDocument? openApiDocument)
    {
        List<RequestDefinition> merged = requests?.ToList() ?? [];

        if (openApiDocument == null)
        {
            return merged;
        }

        List<RequestDefinition> specRequests = BuildRequestsFromOpenApi(openApiDocument);
        if (specRequests.Count == 0)
        {
            return merged;
        }

        HashSet<string> existingNames = new(merged.Select(r => r.Name), StringComparer.OrdinalIgnoreCase);
        foreach (RequestDefinition request in specRequests)
        {
            if (existingNames.Contains(request.Name))
            {
                continue;
            }

            merged.Add(request);
            existingNames.Add(request.Name);
        }

        return merged;
    }

    private static OpenApiDocument? LoadOpenApiDocument(OpenApiSource? source)
    {
        if (source == null || source.Kind != OpenApiSourceKind.File)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(source.Value) || !File.Exists(source.Value))
        {
            return null;
        }

        OpenApiSpecLoader loader = new();
        return loader.LoadAsync(new OpenApiSource(OpenApiSourceKind.File, source.Value), CancellationToken.None)
            .ConfigureAwait(false)
            .GetAwaiter()
            .GetResult();
    }

    private static List<RequestDefinition> BuildRequestsFromOpenApi(OpenApiDocument document)
    {
        string baseUrlTemplate = document.Servers?.Count > 0 ? "{{baseUrl}}" : string.Empty;
        List<RequestDefinition> requests = [];
        HashSet<string> usedNames = new(StringComparer.OrdinalIgnoreCase);

        if (document.Paths == null)
        {
            return requests;
        }

        foreach ((string path, OpenApiPathItem item) in document.Paths)
        {
            if (item == null)
            {
                continue;
            }

            foreach ((OperationType method, OpenApiOperation operation) in item.Operations)
            {
                if (operation == null)
                {
                    continue;
                }

                List<OpenApiParameter> parameters = [];
                if (item.Parameters != null)
                {
                    parameters.AddRange(item.Parameters);
                }
                if (operation.Parameters != null)
                {
                    parameters.AddRange(operation.Parameters);
                }

                List<QueryParamDefinition> queryParams = [.. parameters
                    .Where(p => p.In == ParameterLocation.Query)
                    .Select(p => new QueryParamDefinition(p.Name, $"{{{{{p.Name}}}}}"))];

                List<string> headers = [.. parameters
                    .Where(p => p.In == ParameterLocation.Header)
                    .Select(p => $"{p.Name}: {{{{{p.Name}}}}}")];

                string baseName = string.IsNullOrWhiteSpace(operation.OperationId)
                    ? path
                    : operation.OperationId;
                string name = EnsureUniqueRequestName(baseName, usedNames);

                string url = CombineUrl(baseUrlTemplate, ConvertOpenApiTemplateToVars(path));
                RequestDefinition request = new(
                    name,
                    method.ToString().ToUpperInvariant(),
                    url,
                    null,
                    queryParams,
                    headers,
                    new AuthConfig(AuthMethod.Inherit));

                requests.Add(request);
            }
        }

        return requests;
    }

    private static string EnsureUniqueRequestName(string baseName, HashSet<string> usedNames)
    {
        string name = string.IsNullOrWhiteSpace(baseName) ? "request" : baseName;
        if (!usedNames.Contains(name))
        {
            usedNames.Add(name);
            return name;
        }

        int counter = 2;
        string candidate = name;
        while (usedNames.Contains(candidate))
        {
            candidate = $"{name}-{counter}";
            counter++;
        }

        usedNames.Add(candidate);
        return candidate;
    }

    private static void UpdateVarsFileWithOpenApi(string varsPath, OpenApiDocument? document)
    {
        if (document == null)
        {
            return;
        }

        Dictionary<string, string> vars = LoadVariables(varsPath);
        Dictionary<string, string> specVars = BuildOpenApiVariables(document);
        if (!specVars.ContainsKey(OpenApiTimeoutSecondsKey))
        {
            specVars[OpenApiTimeoutSecondsKey] = DefaultOpenApiTimeoutSeconds.ToString();
        }

        foreach ((string key, string value) in specVars)
        {
            if (!vars.TryGetValue(key, out string? existing) || string.IsNullOrWhiteSpace(existing))
            {
                vars[key] = value;
            }
        }

        EnvironmentYaml yaml = new() { Variables = vars };
        string output = YamlSerializer.Serialize(yaml);
        File.WriteAllText(varsPath, output);
    }

    private static Dictionary<string, string> BuildOpenApiVariables(OpenApiDocument document)
    {
        Dictionary<string, string> vars = new(StringComparer.OrdinalIgnoreCase);

        OpenApiServer? server = document.Servers?.FirstOrDefault();
        if (server != null && !string.IsNullOrWhiteSpace(server.Url))
        {
            vars["baseUrl"] = ConvertOpenApiTemplateToVars(server.Url);

            if (server.Variables != null)
            {
                foreach ((string key, OpenApiServerVariable variable) in server.Variables.Where(kv => !string.IsNullOrWhiteSpace(kv.Key)))
                {
                    vars[key] = variable?.Default ?? string.Empty;
                }
            }

            foreach (string key in ExtractTemplateVariables(server.Url).Where(k => !vars.ContainsKey(k)))
            {
                vars[key] = string.Empty;
            }
        }

        if (document.Paths == null || document.Paths.Count == 0)
        {
            return vars;
        }

        foreach ((string path, OpenApiPathItem item) in document.Paths)
        {
            if (item == null)
            {
                continue;
            }

            foreach (string key in ExtractTemplateVariables(path).Where(k => !vars.ContainsKey(k)))
            {
                vars[key] = string.Empty;
            }

            foreach ((OperationType _, OpenApiOperation operation) in item.Operations)
            {
                List<OpenApiParameter> parameters = [];
                if (item.Parameters != null)
                {
                    parameters.AddRange(item.Parameters);
                }
                if (operation.Parameters != null)
                {
                    parameters.AddRange(operation.Parameters);
                }

                foreach (OpenApiParameter parameter in parameters.Where(p => !string.IsNullOrWhiteSpace(p.Name)))
                {
                    if (vars.TryGetValue(parameter.Name, out string? existing) && !string.IsNullOrWhiteSpace(existing))
                    {
                        continue;
                    }

                    string? defaultValue = parameter.Schema?.Default?.ToString();
                    string? exampleValue = parameter.Example?.ToString();
                    vars[parameter.Name] = defaultValue ?? exampleValue ?? string.Empty;
                }
            }
        }

        return vars;
    }

    private static Dictionary<string, string> LoadVariables(string varsPath)
    {
        if (!File.Exists(varsPath))
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        string yaml = File.ReadAllText(varsPath);
        EnvironmentYaml? existing = YamlDeserializer.Deserialize<EnvironmentYaml>(yaml);
        Dictionary<string, string> vars = new(StringComparer.OrdinalIgnoreCase);

        if (existing?.Variables != null)
        {
            foreach ((string key, string value) in existing.Variables)
            {
                if (!string.IsNullOrWhiteSpace(key))
                {
                    vars[key] = value;
                }
            }
        }

        return vars;
    }

    private static string ConvertOpenApiTemplateToVars(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return input;
        }

        return TemplateVarRegex().Replace(input, "{{$1}}");
    }

    private static IEnumerable<string> ExtractTemplateVariables(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return [];
        }

        return TemplateVarRegex().Matches(input)
            .Select(match => match.Groups[1].Value)
            .Where(value => !string.IsNullOrWhiteSpace(value));
    }

    [GeneratedRegex("\\{([^}]+)\\}")]
    private static partial Regex TemplateVarRegex();

    private static string GetOpenApiFileName(string content, OpenApiSource source)
    {
        string? extension = null;
        if (source.Kind == OpenApiSourceKind.File && !string.IsNullOrWhiteSpace(source.Value))
        {
            string ext = Path.GetExtension(source.Value);
            if (IsOpenApiExtension(ext))
            {
                extension = ext;
            }
        }

        extension ??= LooksLikeJson(content) ? ".json" : ".yaml";
        return $"{OpenApiFileBaseName}{extension}";
    }

    private static bool IsOpenApiExtension(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return false;
        }

        return extension.Equals(".json", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".yaml", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".yml", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeJson(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return false;
        }

        string trimmed = content.TrimStart();
        return trimmed.StartsWith("{", StringComparison.Ordinal) || trimmed.StartsWith("[", StringComparison.Ordinal);
    }

    private static string CombineUrl(string baseUrl, string path)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return path;
        }

        bool baseEndsWithSlash = baseUrl.EndsWith("/", StringComparison.Ordinal);
        bool pathStartsWithSlash = path.StartsWith("/", StringComparison.Ordinal);

        if (baseEndsWithSlash && pathStartsWithSlash)
        {
            return baseUrl.TrimEnd('/') + path;
        }

        if (!baseEndsWithSlash && !pathStartsWithSlash)
        {
            return baseUrl + "/" + path;
        }

        return baseUrl + path;
    }

    private static string DownloadOpenApiSource(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return string.Empty;
        }

        try
        {
            using CancellationTokenSource cts = new(OpenApiDownloadTimeout);
            using HttpClient client = new()
            {
                Timeout = OpenApiDownloadTimeout
            };

            using HttpResponseMessage response = client.GetAsync(url, cts.Token)
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();
            response.EnsureSuccessStatusCode();
            return response.Content.ReadAsStringAsync(cts.Token)
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();
        }
        catch (TaskCanceledException)
        {
            throw new OpenApiSpecificationException("Failed to download OpenAPI spec: request timed out.");
        }
        catch (Exception ex)
        {
            throw new OpenApiSpecificationException($"Failed to download OpenAPI spec: {ex.Message}");
        }
    }

    private static string ReadOpenApiFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new OpenApiSpecificationException("OpenAPI spec file path is required.");
        }

        if (!File.Exists(path))
        {
            throw new OpenApiSpecificationException($"OpenAPI spec file not found: {path}");
        }

        return File.ReadAllText(path);
    }

    private static List<string> WriteRequestFiles(string envDirectory, IReadOnlyList<RequestDefinition> requests)
    {
        List<string> files = [];
        string requestsDir = Path.Combine(envDirectory, RequestsDirectory);
        Directory.CreateDirectory(requestsDir);

        HashSet<string> usedFileNames = new(StringComparer.OrdinalIgnoreCase);

        foreach (RequestDefinition request in requests)
        {
            string fileName = EnsureUniqueFileName(SanitizeFileName(request.Name), requestsDir, usedFileNames);
            string path = Path.Combine(requestsDir, fileName);
            RequestDefinitionDto dto = RequestDefinitionDto.From(request);
            string yaml = YamlSerializer.Serialize(dto);
            File.WriteAllText(path, yaml);
            files.Add(Path.Combine(RequestsDirectory, fileName));
        }

        return files;
    }

    private static List<RequestDefinition> ReadRequestFiles(string envDirectory, IReadOnlyList<string>? requestFiles)
    {
        if (requestFiles == null)
        {
            return [];
        }

        List<RequestDefinition> requests = [.. requestFiles
            .Select(relativePath => Path.Combine(envDirectory, relativePath))
            .Where(path => File.Exists(path))
            .Select(File.ReadAllText)
            .Select(YamlDeserializer.Deserialize<RequestDefinitionDto>)
            .Where(dto => dto is not null)
            .Select(dto => NormalizeRequest(dto.ToRequestDefinition()))];

        return requests;
    }

    private static RequestDefinition NormalizeRequest(RequestDefinition request)
    {
        IReadOnlyList<QueryParamDefinition> queryParams = request.QueryParams ?? new List<QueryParamDefinition>();
        IReadOnlyList<string> headers = request.Headers ?? new List<string>();
        AuthConfig auth = request.Auth ?? new AuthConfig(AuthMethod.Inherit);
        return new RequestDefinition(request.Name, request.Method, request.Url, request.Body, queryParams, headers, auth);
    }

    private static string SanitizeFileName(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(c, '_');
        }

        return string.IsNullOrWhiteSpace(name) ? "request" : name;
    }

    private static string EnsureUniqueFileName(string baseName, string directory, HashSet<string> usedFileNames)
    {
        string candidate = $"{baseName}.yml";
        int counter = 1;

        while (usedFileNames.Contains(candidate) || File.Exists(Path.Combine(directory, candidate)))
        {
            candidate = $"{baseName}-{counter}.yml";
            counter++;
        }

        usedFileNames.Add(candidate);
        return candidate;
    }

    private static string GetGlobalConfigDirectory()
    {
        string? homeEnv = Environment.GetEnvironmentVariable("HOME");
        string home = string.IsNullOrWhiteSpace(homeEnv)
            ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
            : homeEnv;

        if (string.IsNullOrWhiteSpace(home))
        {
            throw new InvalidOperationException("Unable to determine home directory.");
        }

        return Path.Combine(home, ".config", "jubeka");
    }

    private static string GetGlobalEnvDirectory(string name)
    {
        return Path.Combine(GetGlobalConfigDirectory(), name);
    }

    private static string GetConfigPath(string envDirectory)
    {
        return Path.Combine(envDirectory, ConfigFileName);
    }

    private static string GetGlobalCurrentPath()
    {
        return Path.Combine(GetGlobalConfigDirectory(), "current.json");
    }


    private sealed record CurrentEnvironment(string Name);

    private sealed record EnvironmentConfigPersisted(
        string Name,
        string VarsFile,
        OpenApiSourceKind? OpenApiKind,
        string? OpenApiFile,
        List<string> RequestFiles
    );

    private sealed class RequestDefinitionDto
    {
        public string Name { get; set; } = string.Empty;
        public string Method { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string? Body { get; set; }
        public List<QueryParamDefinitionDto> QueryParams { get; set; } = [];
        public List<string> Headers { get; set; } = [];
        public AuthConfigDto Auth { get; set; } = new();

        public static RequestDefinitionDto From(RequestDefinition request)
        {
            return new RequestDefinitionDto
            {
                Name = request.Name,
                Method = request.Method,
                Url = request.Url,
                Body = request.Body,
                QueryParams = [.. request.QueryParams.Select(QueryParamDefinitionDto.From)],
                Headers = [.. request.Headers],
                Auth = AuthConfigDto.From(request.Auth)
            };
        }

        public RequestDefinition ToRequestDefinition()
        {
            List<QueryParamDefinition> queryParams = QueryParams?.Select(q => q.ToQueryParamDefinition()).ToList() ?? [];
            List<string> headers = Headers ?? [];
            AuthConfig auth = Auth?.ToAuthConfig() ?? new AuthConfig(AuthMethod.Inherit);
            return new RequestDefinition(Name, Method, Url, Body, queryParams, headers, auth);
        }
    }

    private sealed class QueryParamDefinitionDto
    {
        public string Key { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;

        public static QueryParamDefinitionDto From(QueryParamDefinition queryParam)
        {
            return new QueryParamDefinitionDto
            {
                Key = queryParam.Key,
                Value = queryParam.Value
            };
        }

        public QueryParamDefinition ToQueryParamDefinition()
        {
            return new QueryParamDefinition(Key, Value);
        }
    }

    private sealed class AuthConfigDto
    {
        public AuthMethod Method { get; set; } = AuthMethod.Inherit;
        public string? Username { get; set; }
        public string? Password { get; set; }
        public string? Token { get; set; }

        public static AuthConfigDto From(AuthConfig auth)
        {
            return new AuthConfigDto
            {
                Method = auth.Method,
                Username = auth.Username,
                Password = auth.Password,
                Token = auth.Token
            };
        }

        public AuthConfig ToAuthConfig()
        {
            return new AuthConfig(Method, Username, Password, Token);
        }
    }

    private const string ConfigFileName = "config.json";
    private const string VarsFileName = "vars.yml";
    private const string OpenApiFileBaseName = "openapi";
    private const string RequestsDirectory = "requests";
    private const int DefaultOpenApiTimeoutSeconds = 30;
    private static readonly TimeSpan OpenApiDownloadTimeout = TimeSpan.FromSeconds(DefaultOpenApiTimeoutSeconds);
    private const string OpenApiTimeoutSecondsKey = "openApiTimeoutSeconds";

    private sealed class EnvironmentYaml
    {
        public Dictionary<string, string>? Variables { get; init; }
    }
}
