namespace Jubeka.Core.Domain;

public sealed record RequestData(
    HttpMethod Method,
    Uri Uri,
    IReadOnlyList<(string Key, string Value)> Headers,
    string? Body = null
);