using Jubeka.Core.Application;
using Jubeka.Core.Application.Default;
using Xunit;

namespace Jubeka.Core.Tests.Application.Default;

public class QueryParserTests
{
    static readonly IQueryParser _parser = new QueryParser();

    [Fact]
    public void ValidKeyValuePairs_AreParsedCorrectly()
    {
        List<string> raw = new() { "key1=value1", "key2=value2" };
        Dictionary<string, string> vars = new();
        List<(string Key, string Value)> parsed = _parser.Parse(raw, vars).ToList();
        Assert.Equal(2, parsed.Count);
        Assert.Equal(("key1", "value1"), parsed[0]);
        Assert.Equal(("key2", "value2"), parsed[1]);
    }

    [Fact]
    public void InvalidPairs_AreSkipped()
    {
        List<string> raw = new() { "key1=", "key", "=value" };
        Dictionary<string, string> vars = new();
        List<(string Key, string Value)> parsed = _parser.Parse(raw, vars).ToList();
        Assert.Equal([], parsed);
    }
}