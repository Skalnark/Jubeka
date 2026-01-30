using System.Collections.Generic;

namespace Jubeka.CLI.Domain;

public sealed record RequestDefinition(
    string Name,
    string Method,
    string Url,
    string? Body,
    IReadOnlyList<string> QueryParams,
    IReadOnlyList<string> Headers
);
