using Jubeka.Core.Domain;

namespace Jubeka.Core.Application.Default;

public sealed class RequestDataBuilder(IBodyLoader bodyLoader,
IHeaderParser headerParser,
IQueryParser queryParser,
IUriBuilderHelper uriBuilder) : IRequestDataBuilder
{
    public RequestData Build(RequestOptions options, IReadOnlyDictionary<string, string> vars)
    {
        string? body = bodyLoader.Load(options.Body);
        IReadOnlyList<(string Key, string Value)> queryParameters = queryParser.Parse(options.QueryParameters, vars);
        IReadOnlyList<(string Key, string Value)> headers = headerParser.Parse(options.Headers, vars);
        Uri uri = uriBuilder.Build(options.Url, vars, queryParameters);

        HttpMethod method = new (options.Method.ToUpperInvariant());
        return new RequestData(method, uri, headers, body);
    }
}