using Jubeka.Core.Application.Default;
using Jubeka.Core.Domain;
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

        [Fact]
        public void Substitute_ReplacesDollarBraces()
        {
            Dictionary<string, string> vars = new() { { "id", "42" } };
            string result = VariableSubstitutor.Substitute("/items/${id}", vars);
            Assert.Equal("/items/42", result);
        }

        [Fact]
        public void GetMissingVariables_ReturnsMissingKeys()
        {
            Dictionary<string, string> vars = new() { { "a", "1" } };
            List<string> missing = VariableSubstitutor.GetMissingVariables("{{a}} ${b}", vars);
            Assert.Single(missing);
            Assert.Contains("b", missing);
        }

        [Fact]
        public void SubstituteOrThrow_WhenMissing_Throws()
        {
            Dictionary<string, string> vars = new();
            Assert.Throws<MissingEnvironmentVariableException>(() => VariableSubstitutor.SubstituteOrThrow("{{missing}}", vars));
        }
    }
}
