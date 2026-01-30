using System.Text.Json;
using Jubeka.CLI.Application;

namespace Jubeka.CLI.Infrastructure.Formatting;

public sealed class JsonResponseFormatter : IResponseFormatter
{
    public string Format(string body, bool pretty)
    {
        if (!pretty || !IsJson(body))
        {
            return body;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(body);
            return JsonSerializer.Serialize(document.RootElement, new JsonSerializerOptions
            {
                WriteIndented = true
            });
        }
        catch
        {
            return body;
        }
    }

    private static bool IsJson(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        try
        {
            // TODO: More efficient way to check for valid JSON?
            JsonSerializer.Serialize(JsonDocument.Parse(input).RootElement);
        }
        catch
        {
            return false;
        }

        return true;
    }
}
