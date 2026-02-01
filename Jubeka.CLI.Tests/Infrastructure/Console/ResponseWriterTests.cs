using System.Net;
using Jubeka.CLI.Application;
using Jubeka.CLI.Infrastructure.Console;
using Jubeka.Core.Domain;

namespace Jubeka.CLI.Tests.Infrastructure.Console;

[Collection("ConsoleTests")]
public class ResponseWriterTests
{
    [Fact]
    public void Write_WritesHeadersAndFormattedBody()
    {
        StubFormatter formatter = new();
        ResponseWriter writer = new(formatter);

        ResponseData response = new(
            StatusCode: HttpStatusCode.OK,
            Headers: [("X-Test", "v1")],
            ContentHeaders: [("Content-Type", "text/plain")],
            Body: "raw",
            IsSuccessStatusCode: true,
            ReasonPhrase: "OK");

        TextWriter originalOut = System.Console.Out;
        using StringWriter output = new();

        try
        {
            System.Console.SetOut(output);

            writer.Write(response, true);

            string text = output.ToString();
            Assert.Contains("HTTP 200 OK", text);
            Assert.Contains("X-Test: v1", text);
            Assert.Contains("Content-Type: text/plain", text);
            Assert.Contains("formatted", text);
        }
        finally
        {
            System.Console.SetOut(originalOut);
        }
    }

    private sealed class StubFormatter : IResponseFormatter
    {
        public string Format(string body, bool pretty) => "formatted";
    }
}
