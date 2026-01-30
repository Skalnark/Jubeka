namespace Jubeka.CLI.Domain;

public sealed record EnvRequestEditOptions(
    string? EnvName,
    bool Local,
    string? RequestName
);
