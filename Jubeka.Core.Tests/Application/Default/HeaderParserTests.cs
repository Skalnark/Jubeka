using System.Collections.Generic;
using System.Linq;
using Jubeka.Core.Application;
using Jubeka.Core.Application.Default;
using Xunit;

namespace Jubeka.Core.Tests.Application.Default
{
    public class HeaderParserTests
    {
        IHeaderParser _parser = new HeaderParser(new VariableSubstitutor());
        [Fact]
        public void ValidHeaders_AreParsedCorrectly()
        {
            var raw = new List<string> { "Header1: Value1", "Header2: Value2" };
            var vars = new Dictionary<string, string>();
            var parsed = _parser.Parse(raw, vars).ToList();
            Assert.Equal(2, parsed.Count);
            Assert.Equal(("Header1", "Value1"), parsed[0]);
            Assert.Equal(("Header2", "Value2"), parsed[1]);
        }
        
        [Fact]
        public void InvalidHeaders_AreSkipped()
        {
            var raw = new List<string> { "Header1 Value1", "Header2-Value2", "Header3:" };
            var vars = new Dictionary<string, string>();
            var parsed = _parser.Parse(raw, vars).ToList();
            Assert.Equal([], parsed);
        }
    }
}
