using System.Collections.Generic;

namespace Jubeka.CLI.Domain;

public sealed record EnvRequestEditOptions(
    string? EnvName,
    string? RequestName,
    bool Inline,
    string? Method,
    string? Url,
    string? Body,
    IReadOnlyList<string> QueryParams,
    IReadOnlyList<string> Headers
);
