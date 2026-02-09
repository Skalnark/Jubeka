using System.Collections.Generic;

namespace Jubeka.CLI.Domain;

public sealed record CLIOptions(
    string Method,
    string Url,
    string? Body,
    string? EnvPath,
    double? TimeoutSeconds,
    bool Pretty,
    IReadOnlyList<string> QueryParams,
    IReadOnlyList<string> Headers
);
