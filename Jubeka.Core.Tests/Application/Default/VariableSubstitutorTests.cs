using Jubeka.Core.Application;
using Jubeka.Core.Application.Default;
using Xunit;

namespace Jubeka.Core.Tests.Application.Default
{
    public class VariableSubstitutorTests
    {
        IVariableSubstitutor substitutor = new VariableSubstitutor();

        [Theory]
        [InlineData("Hello {{name}}", "Hello World", "name", "World")]
        [InlineData("No placeholders here", "No placeholders here", null, null)]
        [InlineData("", "", null, null)]

        public void Substitute_NullOrEmptyOrNoVars_ReturnsExpected(string raw, string expected, string? placeholder, string? var)
        {
            var vars = new Dictionary<string, string>();
            if (placeholder != null && var != null)
            {
                vars[placeholder] = var;
            }

            string result = substitutor.Substitute(raw, vars);

            Assert.Equal(expected, result);
        }

        [Fact]
        public void Substitute_ReplacesPlaceholders_CaseInsensitive()
        {
            var substitutor = new VariableSubstitutor();
            var vars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { { "Name", "World" } };
            string result = substitutor.Substitute("Hello {{name}}", vars);
            Assert.Equal("Hello World", result);
        }
    }
}
