using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Jubeka.CLI.Application;
using Jubeka.CLI.Domain;
using Jubeka.Core.Domain;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Jubeka.CLI.Infrastructure.Config;

public sealed class EnvironmentConfigStore : IEnvironmentConfigStore
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

    public EnvironmentConfig? Get(string name, string? baseDirectory = null)
    {
        if (!string.IsNullOrWhiteSpace(baseDirectory))
        {
            string localDir = GetLocalEnvDirectory(baseDirectory, name);
            EnvironmentConfig? local = LoadFromDirectory(localDir);
            if (local != null)
            {
                return local;
            }
        }

        string globalDir = GetGlobalEnvDirectory(name);
        return LoadFromDirectory(globalDir);
    }

    public void Save(EnvironmentConfig config, bool local = false, string? baseDirectory = null)
    {
        string envDirectory = local
            ? GetLocalEnvDirectory(baseDirectory ?? Directory.GetCurrentDirectory(), config.Name)
            : GetGlobalEnvDirectory(config.Name);
        Directory.CreateDirectory(envDirectory);

        string varsPath = WriteVarsFile(envDirectory, config.VarsPath);
        OpenApiSource? openApiSource = WriteOpenApiSource(envDirectory, config.DefaultOpenApiSource);
        List<string> requestFiles = WriteRequestFiles(envDirectory, config.Requests);

        EnvironmentConfigPersisted persisted = new(
            Name: config.Name,
            VarsFile: Path.GetFileName(varsPath),
            OpenApiKind: openApiSource?.Kind,
            OpenApiFile: openApiSource != null ? OpenApiFileName : null,
            RequestFiles: requestFiles);

        string configPath = GetConfigPath(envDirectory);
        string json = JsonSerializer.Serialize(persisted, SerializerOptions);
        File.WriteAllText(configPath, json);
    }

    public string? GetCurrent(string? baseDirectory = null)
    {
        (string? Name, _) = GetCurrentInfo(baseDirectory);
        return Name;
    }

    public void SetCurrent(string name, bool local = false, string? baseDirectory = null)
    {
        string directory = local
            ? GetLocalConfigDirectory(baseDirectory ?? Directory.GetCurrentDirectory())
            : GetGlobalConfigDirectory();
        Directory.CreateDirectory(directory);

        string path = local
            ? GetLocalCurrentPath(baseDirectory ?? Directory.GetCurrentDirectory())
            : GetGlobalCurrentPath();

        string json = JsonSerializer.Serialize(new CurrentEnvironment(name), SerializerOptions);
        File.WriteAllText(path, json);
    }

    public (string? Name, bool Local) GetCurrentInfo(string? baseDirectory = null)
    {
        if (!string.IsNullOrWhiteSpace(baseDirectory))
        {
            string localPath = GetLocalCurrentPath(baseDirectory);
            if (File.Exists(localPath))
            {
                string json = File.ReadAllText(localPath);
                CurrentEnvironment? current = JsonSerializer.Deserialize<CurrentEnvironment>(json, SerializerOptions);
                return (current?.Name, true);
            }
        }

        string globalPath = GetGlobalCurrentPath();
        if (!File.Exists(globalPath))
        {
            return (null, false);
        }

        string globalJson = File.ReadAllText(globalPath);
        CurrentEnvironment? globalCurrent = JsonSerializer.Deserialize<CurrentEnvironment>(globalJson, SerializerOptions);
        return (globalCurrent?.Name, false);
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
            File.Copy(varsPath, targetPath, true);
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

        string path = Path.Combine(envDirectory, OpenApiFileName);
        switch (source.Kind)
        {
            case OpenApiSourceKind.Url:
                File.WriteAllText(path, source.Value ?? string.Empty);
                return new OpenApiSource(OpenApiSourceKind.Url, source.Value ?? string.Empty);
            case OpenApiSourceKind.Raw:
                File.WriteAllText(path, source.Value ?? string.Empty);
                return new OpenApiSource(OpenApiSourceKind.Raw, source.Value ?? string.Empty);
            case OpenApiSourceKind.File:
                if (!string.IsNullOrWhiteSpace(source.Value) && File.Exists(source.Value))
                {
                    File.Copy(source.Value, path, true);
                }
                else
                {
                    File.WriteAllText(path, string.Empty);
                }

                return new OpenApiSource(OpenApiSourceKind.File, path);
            default:
                return source;
        }
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

    private static List<string> WriteRequestFiles(string envDirectory, IReadOnlyList<RequestDefinition> requests)
    {
        List<string> files = new();
        string requestsDir = Path.Combine(envDirectory, RequestsDirectory);
        Directory.CreateDirectory(requestsDir);

        foreach (RequestDefinition request in requests)
        {
            string fileName = $"{SanitizeFileName(request.Name)}.yml";
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
        List<RequestDefinition> requests = new();
        if (requestFiles == null)
        {
            return requests;
        }

        foreach (string relativePath in requestFiles)
        {
            string path = Path.Combine(envDirectory, relativePath);
            if (!File.Exists(path))
            {
                continue;
            }

            string yaml = File.ReadAllText(path);
            RequestDefinitionDto? dto = YamlDeserializer.Deserialize<RequestDefinitionDto>(yaml);
            if (dto != null)
            {
                requests.Add(NormalizeRequest(dto.ToRequestDefinition()));
            }
        }

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

    private static string GetGlobalConfigDirectory()
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".config", "jubeka");
    }

    private static string GetLocalConfigDirectory(string baseDirectory)
    {
        return Path.Combine(baseDirectory, ".jubeka");
    }

    private static string GetGlobalEnvDirectory(string name)
    {
        return Path.Combine(GetGlobalConfigDirectory(), name);
    }

    private static string GetLocalEnvDirectory(string baseDirectory, string name)
    {
        return Path.Combine(GetLocalConfigDirectory(baseDirectory), name);
    }

    private static string GetConfigPath(string envDirectory)
    {
        return Path.Combine(envDirectory, ConfigFileName);
    }

    private static string GetGlobalCurrentPath()
    {
        return Path.Combine(GetGlobalConfigDirectory(), "current.json");
    }

    private static string GetLocalCurrentPath(string baseDirectory)
    {
        return Path.Combine(GetLocalConfigDirectory(baseDirectory), "current.json");
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
        public List<QueryParamDefinitionDto> QueryParams { get; set; } = new();
        public List<string> Headers { get; set; } = new();
        public AuthConfigDto Auth { get; set; } = new();

        public static RequestDefinitionDto From(RequestDefinition request)
        {
            return new RequestDefinitionDto
            {
                Name = request.Name,
                Method = request.Method,
                Url = request.Url,
                Body = request.Body,
                QueryParams = request.QueryParams.Select(QueryParamDefinitionDto.From).ToList(),
                Headers = request.Headers.ToList(),
                Auth = AuthConfigDto.From(request.Auth)
            };
        }

        public RequestDefinition ToRequestDefinition()
        {
            List<QueryParamDefinition> queryParams = QueryParams?.Select(q => q.ToQueryParamDefinition()).ToList() ?? new List<QueryParamDefinition>();
            List<string> headers = Headers ?? new List<string>();
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
    private const string OpenApiFileName = "openapi.source";
    private const string RequestsDirectory = "requests";
}
