namespace Jubeka.CLI.Domain;

public sealed record AuthConfig(
    AuthMethod Method,
    string? Username = null,
    string? Password = null,
    string? Token = null
);
