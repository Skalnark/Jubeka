namespace Jubeka.CLI.Domain;

public sealed record ParseResult(bool ShowHelp, string? Error, CLIOptions? Options)
{
    public static ParseResult Help(string? error = null) => new(true, error, null);
    public static ParseResult Success(CLIOptions options) => new(false, null, options);
}
