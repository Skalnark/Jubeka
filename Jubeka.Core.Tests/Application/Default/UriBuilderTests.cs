using Jubeka.Core.Application.Default;
using Jubeka.Core.Application;
using Xunit;

namespace Jubeka.Core.Tests.Application.Default
{
    public class UriBuilderTests
    {
        IQueryParser _parser;
        IUriBuilderHelper _builder;

        public UriBuilderTests()
        {
            _parser = new QueryParser();
            _builder = new UriBuilderHelper(_parser);
        }

        [Fact]
        public void ValidUri_IsBuiltCorrectly()
        {
            var rawUrl = "http://example.com/";
            Dictionary<string, string> vars = new() { { "value2", "dynamic" } };
            List<string> queryParameters = ["key1=value1", "key2={{value2}}"];
            IReadOnlyList<(string Key, string Value)> parsedQueryParams = _parser.Parse(queryParameters, vars);

            Uri uri = _builder.Build(rawUrl, vars, parsedQueryParams);

            Assert.Equal("http://example.com/?key1=value1&key2=dynamic", uri.ToString());
        }

        [Fact]
        public void ValidUriWithoutQueryParameters_IsBuiltCorrectly()
        {
            var rawUrl = "http://example.com/api/{{endpoint}}";
            Dictionary<string, string> vars = new() { { "endpoint", "users" } };
            List<string> queryParameters = [];
            IReadOnlyList<(string Key, string Value)> parsedQueryParams = _parser.Parse(queryParameters, vars);

            Uri uri = _builder.Build(rawUrl, vars, parsedQueryParams);

            Assert.Equal("http://example.com/api/users", uri.ToString());
        }

        [Fact]
        public void InvalidUri_ThrowsException()
        {
            var rawUrl = "http://";
            Dictionary<string, string> vars = [];
            List<string> queryParameters = [];
            IReadOnlyList<(string Key, string Value)> parsedQueryParams = _parser.Parse(queryParameters, vars);

            Assert.Throws<UriFormatException>(() => _builder.Build(rawUrl, vars, parsedQueryParams));
        }
    }
}
