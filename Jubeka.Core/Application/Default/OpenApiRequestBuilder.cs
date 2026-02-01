using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using Jubeka.Core.Domain;
using Microsoft.OpenApi.Models;

namespace Jubeka.Core.Application.Default;

public sealed partial class OpenApiRequestBuilder : IOpenApiRequestBuilder
{
    public RequestOptions Build(OpenApiDocument document, string operationId, IReadOnlyDictionary<string, string> vars)
    {
        if (string.IsNullOrWhiteSpace(operationId))
        {
            throw new OpenApiSpecificationException("OperationId is required.");
        }

        (string pathTemplate, OperationType method, OpenApiOperation operation, IList<OpenApiParameter> parameters) =
            FindOperation(document, operationId);

        string baseUrl = ResolveBaseUrl(document, vars);
        string resolvedPath = ResolvePath(pathTemplate, vars, parameters);

        List<string> queryParams = BuildQueryParams(parameters, vars);
        List<string> headers = BuildHeaders(parameters, vars);
        string? body = BuildBody(operation, vars, operationId);

        string url = CombineUrl(baseUrl, resolvedPath);
        return new RequestOptions(
            Method: method.ToString().ToUpperInvariant(),
            Url: VariableSubstitutor.SubstituteOrThrow(url, vars),
            Body: body,
            QueryParameters: queryParams,
            Headers: headers
        );
    }

    private static (string PathTemplate, OperationType Method, OpenApiOperation Operation, IList<OpenApiParameter> Parameters)
        FindOperation(OpenApiDocument document, string operationId)
    {
        foreach ((string path, OpenApiPathItem pathItem) in document.Paths)
        {
            foreach ((OperationType method, OpenApiOperation operation) in pathItem.Operations)
            {
                if (string.Equals(operation.OperationId, operationId, StringComparison.OrdinalIgnoreCase))
                {
                    List<OpenApiParameter> parameters = [];
                    if (pathItem.Parameters != null)
                    {
                        parameters.AddRange(pathItem.Parameters);
                    }

                    if (operation.Parameters != null)
                    {
                        parameters.AddRange(operation.Parameters);
                    }

                    return (path, method, operation, parameters);
                }
            }
        }

        throw new OpenApiSpecificationException($"OperationId not found: {operationId}");
    }

    private static string ResolveBaseUrl(OpenApiDocument document, IReadOnlyDictionary<string, string> vars)
    {
        string? serverUrl = document.Servers.FirstOrDefault()?.Url;
        if (string.IsNullOrWhiteSpace(serverUrl))
        {
            if (vars.TryGetValue("baseUrl", out string? envBaseUrl) && !string.IsNullOrWhiteSpace(envBaseUrl))
            {
                return envBaseUrl;
            }

            return string.Empty;
        }

        return VariableSubstitutor.SubstituteOrThrow(serverUrl, vars);
    }

    private static string ResolvePath(string pathTemplate, IReadOnlyDictionary<string, string> vars, IList<OpenApiParameter> parameters)
    {
        string resolved = pathTemplate;
        foreach (string name in PathParameterRegex().Matches(pathTemplate).Cast<Match>().Select(match => match.Groups["name"].Value))
        {
            if (!vars.TryGetValue(name, out string? value))
            {
                bool required = parameters.Any(p => p.In == ParameterLocation.Path && string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase) && p.Required);
                if (required)
                {
                    throw new MissingEnvironmentVariableException([name]);
                }

                continue;
            }

            resolved = resolved.Replace($"{{{name}}}", WebUtility.UrlEncode(value));
        }

        return resolved;
    }

    private static List<string> BuildQueryParams(IList<OpenApiParameter> parameters, IReadOnlyDictionary<string, string> vars)
    {
        List<string> query = [];
        foreach (OpenApiParameter parameter in parameters.Where(p => p.In == ParameterLocation.Query))
        {
            if (vars.TryGetValue(parameter.Name, out string? value))
            {
                query.Add($"{parameter.Name}={VariableSubstitutor.Substitute(value, vars)}");
            }
            else if (parameter.Required)
            {
                throw new MissingEnvironmentVariableException([parameter.Name]);
            }
        }

        return query;
    }

    private static List<string> BuildHeaders(IList<OpenApiParameter> parameters, IReadOnlyDictionary<string, string> vars)
    {
        List<string> headers = [];
        foreach (OpenApiParameter parameter in parameters.Where(p => p.In == ParameterLocation.Header))
        {
            if (vars.TryGetValue(parameter.Name, out string? value))
            {
                headers.Add($"{parameter.Name}: {VariableSubstitutor.Substitute(value, vars)}");
            }
            else if (parameter.Required)
            {
                throw new MissingEnvironmentVariableException([parameter.Name]);
            }
        }

        return headers;
    }

    private static string? BuildBody(OpenApiOperation operation, IReadOnlyDictionary<string, string> vars, string operationId)
    {
        if (operation.RequestBody == null)
        {
            return null;
        }

        if (vars.TryGetValue($"{operationId}.body", out string? opBody))
        {
            return VariableSubstitutor.Substitute(opBody, vars);
        }

        if (vars.TryGetValue("body", out string? body))
        {
            return VariableSubstitutor.Substitute(body, vars);
        }

        if (operation.RequestBody.Required)
        {
            throw new MissingEnvironmentVariableException(["body"]);
        }

        return null;
    }

    private static string CombineUrl(string baseUrl, string path)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            if (Uri.TryCreate(path, UriKind.Absolute, out Uri? absolute))
            {
                return absolute.ToString();
            }

            throw new OpenApiSpecificationException("OpenAPI document does not define a server URL and no baseUrl variable was provided.");
        }

        if (!baseUrl.EndsWith('/'))
        {
            baseUrl += "/";
        }

        string trimmedPath = path.TrimStart('/');
        return baseUrl + trimmedPath;
    }

    [GeneratedRegex(@"\{(?<name>[^}]+)\}")]
    private static partial Regex PathParameterRegex();
}
