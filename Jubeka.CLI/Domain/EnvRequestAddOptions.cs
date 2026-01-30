using System.Collections.Generic;

namespace Jubeka.CLI.Domain;

public sealed record EnvRequestAddOptions(
    string EnvName,
    bool Local,
    string? Name,
    string? Method,
    string? Url,
    string? Body,
    IReadOnlyList<string> QueryParams,
    IReadOnlyList<string> Headers
);
