using Jubeka.CLI.Application;
using Jubeka.Core.Domain;

namespace Jubeka.CLI.Infrastructure.Console;

public sealed class ResponseWriter(IResponseFormatter formatter) : IResponseWriter
{
    private readonly IResponseFormatter _formatter = formatter;

    public void Write(ResponseData response, bool pretty)
    {
        System.Console.WriteLine($"HTTP {(int)response.StatusCode} {response.ReasonPhrase}");

        foreach ((string? name, string? value) in response.Headers)
        {
            System.Console.WriteLine($"{name}: {value}");
        }

        foreach ((string? name, string? value) in response.ContentHeaders)
        {
            System.Console.WriteLine($"{name}: {value}");
        }

        System.Console.WriteLine();
        System.Console.WriteLine(_formatter.Format(response.Body, pretty));
    }
}
