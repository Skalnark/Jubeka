namespace Jubeka.Core.Domain;

public sealed record RequestData(
    HttpMethod Method,
    Uri Uri,
    string? Body = null,
    IReadOnlyList<(string Key, string Value)> Headers
);