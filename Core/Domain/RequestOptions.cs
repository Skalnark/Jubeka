namespace Jubeka.Core.Domain;

public sealed record RequestOptions(
    string Method,
    string Url,
    string? Body = null,
    IReadOnlyList<string> QueryParameters = null,
    IReadOnlyList<(string Key, string Value)> Headers = null
);