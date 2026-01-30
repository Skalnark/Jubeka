using Jubeka.Core.Application.Default;
using Jubeka.Core.Domain;
using Jubeka.Core.Infraestructure.IO;
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

                var builder = new RequestDataBuilder(bodyLoader, headerParser, queryParser, uriBuilder);
                var options = new RequestOptions(
                    Method: "post",
                    Url: "https://example.test/api/{{id}}",
                    Body: "@" + tmp,
                    QueryParameters: new List<string> { "p=1" },
                    Headers: new List<string> { "X-Custom: v" }
                );

                var vars = new Dictionary<string, string> { { "id", "100" } };
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
