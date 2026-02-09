namespace Jubeka.CLI.Domain;

public sealed record EnvRequestExecOptions(
    string? EnvName,
    string? RequestName,
    double? TimeoutSeconds
);
