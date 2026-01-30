using Jubeka.Core.Application.Default;
using Xunit;

namespace Jubeka.Core.Tests.Application.Default
{
    public class VariableSubstitutorTests
    {
        [Theory]
        [InlineData("Hello {{name}}", "Hello World", "name", "World")]
        [InlineData("No placeholders here", "No placeholders here", null, null)]
        [InlineData("", "", null, null)]

        public void Substitute_NullOrEmptyOrNoVars_ReturnsExpected(string raw, string expected, string? placeholder, string? var)
        {
            Dictionary<string, string> vars = [];
            if (placeholder != null && var != null)
            {
                vars[placeholder] = var;
            }

            string result = VariableSubstitutor.Substitute(raw, vars);

            Assert.Equal(expected, result);
        }

        [Fact]
        public void Substitute_ReplacesPlaceholders_CaseInsensitive()
        {
            Dictionary<string, string> vars = new(StringComparer.OrdinalIgnoreCase) { { "Name", "World" } };
            string result = VariableSubstitutor.Substitute("Hello {{name}}", vars);
            Assert.Equal("Hello World", result);
        }
    }
}
