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
        Assert.Equal(CliCommand.Request, result.Command);
        RequestCommandOptions options = Assert.IsType<RequestCommandOptions>(result.Options);
        Assert.Equal("GET", options.Method);
        Assert.Equal("https://example.com", options.Url);
        Assert.Single(options.QueryParams);
        Assert.Single(options.Headers);
    }

    [Fact]
    public void Parse_BodyAndPrettyAndTimeout_ParsesValues()
    {
        ArgumentParser parser = new();

        ParseResult result = parser.Parse([
            "--method", "POST",
            "--url", "https://example.com",
            "--body", "{\"a\":1}",
            "--pretty",
            "--timeout", "2.5"
        ]);

        Assert.False(result.ShowHelp);
        Assert.NotNull(result.Options);
        RequestCommandOptions options = Assert.IsType<RequestCommandOptions>(result.Options);
        Assert.Equal("POST", options.Method);
        Assert.Equal("https://example.com", options.Url);
        Assert.True(options.Pretty);
        Assert.Equal(2.5, options.TimeoutSeconds);
    }

    [Fact]
    public void Parse_HelpFlag_ReturnsHelp()
    {
        ArgumentParser parser = new();

        ParseResult result = parser.Parse(["-h"]);

        Assert.True(result.ShowHelp);
        Assert.Null(result.Options);
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

    [Fact]
    public void Parse_OpenApiRequest_ParsesOptions()
    {
        ArgumentParser parser = new();

        ParseResult result = parser.Parse([
            "openapi",
            "request",
            "--operation", "getPet",
            "--spec-url", "https://example.com/openapi.json",
            "--env", "vars.yml"
        ]);

        Assert.False(result.ShowHelp);
        Assert.Equal(CliCommand.OpenApiRequest, result.Command);
        OpenApiCommandOptions options = Assert.IsType<OpenApiCommandOptions>(result.Options);
        Assert.Equal("getPet", options.OperationId);
        Assert.NotNull(options.Source);
        Assert.Equal("vars.yml", options.EnvPath);
    }

    [Fact]
    public void Parse_EnvCreate_ParsesOptions()
    {
        ArgumentParser parser = new();

        ParseResult result = parser.Parse([
            "env",
            "create",
            "--name", "dev",
            "--vars", "vars.yml",
            "--spec-file", "spec.yaml"
        ]);

        Assert.False(result.ShowHelp);
        Assert.Equal(CliCommand.EnvCreate, result.Command);
        EnvConfigOptions options = Assert.IsType<EnvConfigOptions>(result.Options);
        Assert.Equal("dev", options.Name);
        Assert.Equal("vars.yml", options.VarsPath);
        Assert.NotNull(options.DefaultOpenApiSource);
    }

    [Fact]
    public void Parse_EnvRequestAdd_ParsesOptions()
    {
        ArgumentParser parser = new();

        ParseResult result = parser.Parse([
            "env",
            "request",
            "add",
            "--name", "dev",
            "--req-name", "Ping",
            "--method", "GET",
            "--url", "https://example.com/ping",
            "--query", "q=1",
            "--header", "X-Test: v"
        ]);

        Assert.False(result.ShowHelp);
        Assert.Equal(CliCommand.EnvRequestAdd, result.Command);
        EnvRequestAddOptions options = Assert.IsType<EnvRequestAddOptions>(result.Options);
        Assert.Equal("dev", options.EnvName);
        Assert.Equal("Ping", options.Name);
        Assert.Equal("GET", options.Method);
        Assert.Equal("https://example.com/ping", options.Url);
        Assert.Single(options.QueryParams);
        Assert.Single(options.Headers);
    }

    [Fact]
    public void Parse_EnvRequestList_ParsesOptions()
    {
        ArgumentParser parser = new();

        ParseResult result = parser.Parse([
            "env",
            "request",
            "list",
            "--name", "dev"
        ]);

        Assert.Equal(CliCommand.EnvRequestList, result.Command);
        EnvRequestListOptions options = Assert.IsType<EnvRequestListOptions>(result.Options);
        Assert.Equal("dev", options.EnvName);
    }

    [Fact]
    public void Parse_EnvRequestEdit_ParsesOptions()
    {
        ArgumentParser parser = new();

        ParseResult result = parser.Parse([
            "env",
            "request",
            "edit",
            "--name", "dev",
            "--req-name", "Ping"
        ]);

        Assert.Equal(CliCommand.EnvRequestEdit, result.Command);
        EnvRequestEditOptions options = Assert.IsType<EnvRequestEditOptions>(result.Options);
        Assert.Equal("dev", options.EnvName);
        Assert.Equal("Ping", options.RequestName);
        Assert.False(options.Inline);
    }

    [Fact]
    public void Parse_EnvRequestEdit_WithInline_ParsesOptions()
    {
        ArgumentParser parser = new();

        ParseResult result = parser.Parse([
            "env",
            "request",
            "edit",
            "--req-name", "Ping",
            "--inline",
            "--method", "POST",
            "--url", "https://example.com/ping",
            "--body", "{}",
            "--query", "q=1",
            "--header", "X-Test: v"
        ]);

        Assert.Equal(CliCommand.EnvRequestEdit, result.Command);
        EnvRequestEditOptions options = Assert.IsType<EnvRequestEditOptions>(result.Options);
        Assert.Equal("Ping", options.RequestName);
        Assert.True(options.Inline);
        Assert.Equal("POST", options.Method);
        Assert.Equal("https://example.com/ping", options.Url);
        Assert.Equal("{}", options.Body);
        Assert.Single(options.QueryParams);
        Assert.Single(options.Headers);
    }

    [Fact]
    public void Parse_EnvRequestList_WithoutName_AllowsCurrent()
    {
        ArgumentParser parser = new();

        ParseResult result = parser.Parse([
            "env",
            "request",
            "list"
        ]);

        Assert.Equal(CliCommand.EnvRequestList, result.Command);
        EnvRequestListOptions options = Assert.IsType<EnvRequestListOptions>(result.Options);
        Assert.Null(options.EnvName);
    }

    [Fact]
    public void Parse_EnvSet_ParsesOptions()
    {
        ArgumentParser parser = new();

        ParseResult result = parser.Parse([
            "env",
            "set",
            "--name", "dev"
        ]);

        Assert.Equal(CliCommand.EnvSet, result.Command);
        EnvSetOptions options = Assert.IsType<EnvSetOptions>(result.Options);
        Assert.Equal("dev", options.Name);
    }

    [Fact]
    public void Parse_EnvRequestExec_ParsesOptions()
    {
        ArgumentParser parser = new();

        ParseResult result = parser.Parse([
            "env",
            "request",
            "exec",
            "--name", "dev",
            "--req-name", "Ping"
        ]);

        Assert.Equal(CliCommand.EnvRequestExec, result.Command);
        EnvRequestExecOptions options = Assert.IsType<EnvRequestExecOptions>(result.Options);
        Assert.Equal("dev", options.EnvName);
        Assert.Equal("Ping", options.RequestName);
    }
}
