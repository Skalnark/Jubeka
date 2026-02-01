using System.Collections.Generic;

namespace Jubeka.CLI.Domain;

public sealed record RequestDefinition(
    string Name,
    string Method,
    string Url,
    string? Body,
    IReadOnlyList<QueryParamDefinition> QueryParams,
    IReadOnlyList<string> Headers,
    AuthConfig Auth
);
