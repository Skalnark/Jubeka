namespace Jubeka.CLI.Domain;

public sealed record EnvRequestListOptions(
    string? EnvName,
    bool Local
);
