using Jubeka.CLI.Application.Default;
using Jubeka.CLI.Domain;

namespace Jubeka.CLI.Tests.Application.Default;

public class ArgumentParserTests
{
    [Fact]
    public void Parse_NoArgs_ReturnsHelp()
    {
        ArgumentParser parser = new();

        ParseResult result = parser.Parse([]);

        Assert.True(result.ShowHelp);
        Assert.Null(result.Options);
    }

    [Fact]
    public void Parse_RequestCommand_IgnoresPrefixAndParsesOptions()
    {
        ArgumentParser parser = new();

        ParseResult result = parser.Parse([
            "request",
            "--method", "GET",
            "--url", "https://example.com",
            "--query", "q=1",
            "--header", "X-Test: v"
        ]);

        Assert.False(result.ShowHelp);
        Assert.NotNull(result.Options);
        Assert.Equal("GET", result.Options!.Method);
        Assert.Equal("https://example.com", result.Options.Url);
        Assert.Single(result.Options.QueryParams);
        Assert.Single(result.Options.Headers);
    }

    [Fact]
    public void Parse_InvalidTimeout_ReturnsHelpWithError()
    {
        ArgumentParser parser = new();

        ParseResult result = parser.Parse([
            "--method", "GET",
            "--url", "https://example.com",
            "--timeout", "-1"
        ]);

        Assert.True(result.ShowHelp);
        Assert.Contains("Invalid timeout", result.Error);
    }

    [Fact]
    public void Parse_UnknownArgument_ReturnsHelpWithError()
    {
        ArgumentParser parser = new();

        ParseResult result = parser.Parse([
            "--method", "GET",
            "--url", "https://example.com",
            "--nope"
        ]);

        Assert.True(result.ShowHelp);
        Assert.Contains("Unknown argument", result.Error);
    }

    [Fact]
    public void Parse_MissingRequiredFields_ReturnsHelp()
    {
        ArgumentParser parser = new();

        ParseResult result = parser.Parse(["--method", "GET"]);

        Assert.True(result.ShowHelp);
        Assert.Contains("Both --method and --url are required", result.Error);
    }
}
