using Jubeka.Core.Domain;

namespace Jubeka.Core.Application.Default;

public sealed class RequestDataBuilder(IBodyLoader bodyLoader,
IHeaderParser headerParser,
IQueryParser queryParser,
IUriBuilderHelper uriBuilder) : IRequestDataBuilder
{
    public RequestData Build(RequestOptions options, IReadOnlyDictionary<string, string> vars)
    {
        ValidateMissingVariables(options, vars);
        string? body = bodyLoader.Load(options.Body);
        body = VariableSubstitutor.SubstituteOrThrow(body, vars);
        IReadOnlyList<(string Key, string Value)> queryParameters = queryParser.Parse(options.QueryParameters, vars);
        IReadOnlyList<(string Key, string Value)> headers = headerParser.Parse(options.Headers, vars);
        Uri uri = uriBuilder.Build(options.Url, vars, queryParameters);

        HttpMethod method = new (options.Method.ToUpperInvariant());
        return new RequestData(method, uri, headers, body);
    }

    private static void ValidateMissingVariables(RequestOptions options, IReadOnlyDictionary<string, string> vars)
    {
        List<string> missing = [];
        missing.AddRange(VariableSubstitutor.GetMissingVariables(options.Url, vars));
        missing.AddRange(VariableSubstitutor.GetMissingVariables(options.Body, vars));

        if (options.QueryParameters != null)
        {
            foreach (string raw in options.QueryParameters)
            {
                int separatorIndex = raw.IndexOf('=');
                if (separatorIndex >= 1)
                {
                    string value = raw.Substring(separatorIndex + 1);
                    missing.AddRange(VariableSubstitutor.GetMissingVariables(value, vars));
                }
            }
        }

        if (options.Headers != null)
        {
            foreach (string raw in options.Headers)
            {
                int separatorIndex = raw.IndexOf(':');
                if (separatorIndex >= 1)
                {
                    string value = raw.Substring(separatorIndex + 1);
                    missing.AddRange(VariableSubstitutor.GetMissingVariables(value, vars));
                }
            }
        }

        if (missing.Count > 0)
        {
            throw new Jubeka.Core.Domain.MissingEnvironmentVariableException(missing.Distinct(StringComparer.OrdinalIgnoreCase).ToList());
        }
    }
}