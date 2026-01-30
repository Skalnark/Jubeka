namespace Jubeka.CLI.Domain;

public sealed record ParseResult(bool ShowHelp, string? Error, CliCommand? Command, object? Options)
{
    public static ParseResult Help(string? error = null) => new(true, error, null, null);
    public static ParseResult Success(CliCommand command, object options) => new(false, null, command, options);
}
