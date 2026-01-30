using Jubeka.Core.Application.Default;
using Jubeka.Core.Domain;
using Jubeka.Core.Infrastructure.IO;
using Xunit;

namespace Jubeka.Core.Tests.Application.Default
{
    public class RequestDataBuilderTests
    {
        [Fact]
        public void Build_ConstructsRequestData()
        {
            string tmp = Path.GetTempFileName();
            try
            {
                File.WriteAllText(tmp, "payload");
                BodyLoader bodyLoader = new();
                HeaderParser headerParser = new();
                QueryParser queryParser = new();
                UriBuilderHelper uriBuilder = new(queryParser);

                RequestDataBuilder builder = new(bodyLoader, headerParser, queryParser, uriBuilder);
                RequestOptions options = new(
                    Method: "post",
                    Url: "https://example.test/api/{{id}}",
                    Body: "@" + tmp,
                    QueryParameters: ["p=1"],
                    Headers: ["X-Custom: v"]
                );

                Dictionary<string, string> vars = new() { { "id", "100" } };
                RequestData data = builder.Build(options, vars);

                Assert.Equal("POST", data.Method.Method);
                Assert.Equal("payload", data.Body);
                Assert.Contains("/api/100", data.Uri.AbsoluteUri);
                Assert.Contains("p=1", data.Uri.Query);
                Assert.Contains(data.Headers, h => h.Key == "X-Custom" && h.Value == "v");
            }
            finally
            {
                if (File.Exists(tmp)) File.Delete(tmp);
            }
        }
    }
}
