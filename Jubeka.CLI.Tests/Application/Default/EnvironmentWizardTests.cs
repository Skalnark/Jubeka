using Jubeka.CLI.Application;
using Jubeka.CLI.Application.Default;
using Jubeka.CLI.Domain;
using Jubeka.Core.Domain;

namespace Jubeka.CLI.Tests.Application.Default;

public class EnvironmentWizardTests
{
    [Fact]
    public void BuildEnvironmentConfig_WithSpecUrl_UsesPromptValues()
    {
        StubPrompt prompt = new(
            required: ["dev", "url", "https://example.com/openapi.json"],
            withDefault: ["vars.yml"],
            yesNo: [true]);
        EnvironmentWizard wizard = new(prompt);
        EnvConfigOptions options = new("", "", null);

        EnvironmentConfig config = wizard.BuildEnvironmentConfig(options, "create");

        Assert.Equal("dev", config.Name);
        Assert.Equal("vars.yml", config.VarsPath);
        Assert.NotNull(config.DefaultOpenApiSource);
        Assert.Equal(OpenApiSourceKind.Url, config.DefaultOpenApiSource!.Kind);
        Assert.Equal("https://example.com/openapi.json", config.DefaultOpenApiSource.Value);
    }

    [Fact]
    public void BuildEnvironmentConfig_WhenSkipSpec_ReturnsNullSpec()
    {
        StubPrompt prompt = new(
            required: ["prod"],
            withDefault: ["prod.yml"],
            yesNo: [false]);
        EnvironmentWizard wizard = new(prompt);
        EnvConfigOptions options = new("", "", null);

        EnvironmentConfig config = wizard.BuildEnvironmentConfig(options, "create");

        Assert.Equal("prod", config.Name);
        Assert.Equal("prod.yml", config.VarsPath);
        Assert.Null(config.DefaultOpenApiSource);
    }

    private sealed class StubPrompt : IPrompt
    {
        private readonly Queue<string> _required;
        private readonly Queue<string?> _optional;
        private readonly Queue<string> _withDefault;
        private readonly Queue<bool?> _yesNo;

        public StubPrompt(IEnumerable<string> required, IEnumerable<string>? withDefault = null, IEnumerable<bool?>? yesNo = null, IEnumerable<string?>? optional = null)
        {
            _required = new Queue<string>(required);
            _withDefault = new Queue<string>(withDefault ?? Array.Empty<string>());
            _yesNo = new Queue<bool?>(yesNo ?? Array.Empty<bool?>());
            _optional = new Queue<string?>(optional ?? Array.Empty<string?>());
        }

        public string PromptWithDefault(string label, string? defaultValue)
        {
            return _withDefault.Count > 0 ? _withDefault.Dequeue() : (defaultValue ?? string.Empty);
        }

        public string? PromptOptional(string label, string? defaultValue)
        {
            return _optional.Count > 0 ? _optional.Dequeue() : defaultValue;
        }

        public string PromptRequired(string label, string? defaultValue)
        {
            return _required.Count > 0 ? _required.Dequeue() : (defaultValue ?? string.Empty);
        }

        public bool? PromptYesNo(string label, bool? defaultValue)
        {
            return _yesNo.Count > 0 ? _yesNo.Dequeue() : defaultValue;
        }
    }
}
