using Jubeka.CLI.Application;
using Jubeka.CLI.Application.Default;
using Jubeka.CLI.Domain;

namespace Jubeka.CLI.Tests.Application.Default;

public class RequestWizardTests
{
    [Fact]
    public void BuildRequest_UsesPromptValues()
    {
        StubPrompt prompt = new(
            required: ["ping", "GET", "https://api.example.com/ping"],
            optional: [null],
            withDefault: ["5", "4", "1"]); // query done, headers done, auth inherit
        RequestWizard wizard = new(prompt);
        EnvRequestAddOptions options = new(
            EnvName: null,
            Name: null,
            Method: null,
            Url: null,
            Body: null,
            QueryParams: Array.Empty<string>(),
            Headers: Array.Empty<string>());

        RequestDefinition request = wizard.BuildRequest(options, new Dictionary<string, string>());

        Assert.Equal("ping", request.Name);
        Assert.Equal("GET", request.Method);
        Assert.Equal("https://api.example.com/ping", request.Url);
        Assert.Null(request.Body);
        Assert.Empty(request.QueryParams);
        Assert.Empty(request.Headers);
        Assert.Equal(AuthMethod.Inherit, request.Auth.Method);
    }

    [Fact]
    public void EditRequest_WhenSaveSelected_ReturnsUpdatedRequest()
    {
        StubPrompt prompt = new(
            withDefault: ["8"],
            required: [],
            optional: []);
        RequestWizard wizard = new(prompt);

        RequestDefinition original = new(
            Name: "orig",
            Method: "POST",
            Url: "https://api.example.com/old",
            Body: "body",
            QueryParams: [],
            Headers: ["H: 1"],
            Auth: new AuthConfig(AuthMethod.None));

        RequestDefinition edited = wizard.EditRequest(original, new Dictionary<string, string>());

        Assert.Equal("orig", edited.Name);
        Assert.Equal("POST", edited.Method);
        Assert.Equal("https://api.example.com/old", edited.Url);
        Assert.Equal("body", edited.Body);
        Assert.Equal(AuthMethod.None, edited.Auth.Method);
        Assert.NotSame(original, edited);
    }

    [Fact]
    public void BuildRequest_WhenVariableSelectionCancelled_KeepsWizardAlive()
    {
        StubPrompt prompt = new(
            required: ["ping", "GET", "https://api.example.com/ping", "id"],
            optional: [null],
            withDefault: ["4", string.Empty, "5", "4", "1"]); // select existing var, cancel selection, done queries, done headers, auth inherit
        RequestWizard wizard = new(prompt);
        EnvRequestAddOptions options = new(
            EnvName: null,
            Name: null,
            Method: null,
            Url: null,
            Body: null,
            QueryParams: Array.Empty<string>(),
            Headers: Array.Empty<string>());

        RequestDefinition request = wizard.BuildRequest(options, new Dictionary<string, string>
        {
            { "id", "123" }
        });

        Assert.Empty(request.QueryParams);
        Assert.Equal("ping", request.Name);
    }

    private sealed class StubPrompt : IPrompt
    {
        private readonly Queue<string> _required;
        private readonly Queue<string?> _optional;
        private readonly Queue<string> _withDefault;
        private readonly Queue<bool?> _yesNo;

        public StubPrompt(IEnumerable<string>? required = null, IEnumerable<string?>? optional = null, IEnumerable<string>? withDefault = null, IEnumerable<bool?>? yesNo = null)
        {
            _required = new Queue<string>(required ?? Array.Empty<string>());
            _optional = new Queue<string?>(optional ?? Array.Empty<string?>());
            _withDefault = new Queue<string>(withDefault ?? Array.Empty<string>());
            _yesNo = new Queue<bool?>(yesNo ?? Array.Empty<bool?>());
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
