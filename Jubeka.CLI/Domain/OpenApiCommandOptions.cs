using Jubeka.Core.Domain;

namespace Jubeka.CLI.Domain;

public sealed record OpenApiCommandOptions(
    string OperationId,
    OpenApiSource? Source,
    string? EnvPath,
    string? EnvName,
    double? TimeoutSeconds,
    bool Pretty
);
