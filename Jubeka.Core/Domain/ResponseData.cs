using System.Net;

namespace Jubeka.Core.Domain;

public sealed record ResponseData(
    HttpStatusCode StatusCode,
    IReadOnlyList<(string Key, string Value)> Headers,
    IReadOnlyList<(string Key, string Value)> ContentHeaders,
    string Body,
    bool IsSuccessStatusCode,
    string? ReasonPhrase = null,
    TimeSpan? Elapsed = null
);