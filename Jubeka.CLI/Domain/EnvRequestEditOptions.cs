namespace Jubeka.CLI.Domain;

public sealed record EnvRequestEditOptions(
    string? EnvName,
    string? RequestName
);
