using Jubeka.Core.Application;
using Jubeka.Core.Application.Default;
using Xunit;

namespace Jubeka.Core.Tests.Application.Default
{
    public class HeaderParserTests
    {
        IHeaderParser _parser = new HeaderParser();
        [Fact]
        public void ValidHeaders_AreParsedCorrectly()
        {
            List<string> raw = ["Header1: Value1", "Header2: Value2"];
            Dictionary<string, string> vars = [];
            List<(string Key, string Value)> parsed = _parser.Parse(raw, vars).ToList();
            Assert.Equal(2, parsed.Count);
            Assert.Equal(("Header1", "Value1"), parsed[0]);
            Assert.Equal(("Header2", "Value2"), parsed[1]);
        }

        [Fact]
        public void InvalidHeaders_AreSkipped()
        {
            List<string> raw = ["Header1 Value1", "Header2-Value2", "Header3:"];
            Dictionary<string, string> vars = [];
            List<(string Key, string Value)> parsed = _parser.Parse(raw, vars).ToList();
            Assert.Equal([], parsed);
        }
    }
}
