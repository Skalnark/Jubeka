using Jubeka.Core.Application;
using Jubeka.Core.Application.Default;
using Xunit;

namespace Jubeka.Core.Tests.Application.Default;

public class QueryParserTests
{
    static readonly IQueryParser _parser = new QueryParser(new VariableSubstitutor());

    [Fact]
    public void ValidKeyValuePairs_AreParsedCorrectly()
    {
        var raw = new List<string> { "key1=value1", "key2=value2" };
        var vars = new Dictionary<string, string>();
        var parsed = _parser.Parse(raw, vars).ToList();
        Assert.Equal(2, parsed.Count);
        Assert.Equal(("key1", "value1"), parsed[0]);
        Assert.Equal(("key2", "value2"), parsed[1]);
    }

    [Fact]
    public void InvalidPairs_AreSkipped()
    {
        var raw = new List<string> { "key1=", "key", "=value" };
        var vars = new Dictionary<string, string>();
        var parsed = _parser.Parse(raw, vars).ToList();
        Assert.Equal([], parsed);
    }
}