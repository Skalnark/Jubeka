using System.Text;
using Jubeka.Core.Application;
using Jubeka.Core.Domain;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Readers;

namespace Jubeka.Core.Infrastructure.OpenApi;

public sealed class OpenApiSpecLoader : IOpenApiSpecLoader
{
    public async Task<OpenApiDocument> LoadAsync(OpenApiSource source, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(source.Value))
        {
            throw new OpenApiSpecificationException("OpenAPI source is empty.");
        }

        return source.Kind switch
        {
            OpenApiSourceKind.Url => await LoadFromUrlAsync(source.Value, cancellationToken).ConfigureAwait(false),
            OpenApiSourceKind.File => LoadFromFile(source.Value),
            OpenApiSourceKind.Raw => LoadFromRaw(source.Value),
            _ => throw new OpenApiSpecificationException("Unknown OpenAPI source kind.")
        };
    }

    private static async Task<OpenApiDocument> LoadFromUrlAsync(string url, CancellationToken cancellationToken)
    {
        using HttpClient client = new();
        string content = await client.GetStringAsync(url, cancellationToken).ConfigureAwait(false);
        return LoadFromRaw(content);
    }

    private static OpenApiDocument LoadFromFile(string path)
    {
        if (!File.Exists(path))
        {
            throw new OpenApiSpecificationException($"OpenAPI file not found: {path}");
        }

        string content = NormalizeIndentation(File.ReadAllText(path, Encoding.UTF8));
        return LoadFromRaw(content);
    }

    private static OpenApiDocument LoadFromRaw(string raw)
    {
        string normalized = NormalizeIndentation(raw);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new OpenApiSpecificationException("OpenAPI raw content is empty.");
        }

        try
        {
            OpenApiStringReader reader = new();
            OpenApiDocument document = reader.Read(normalized, out OpenApiDiagnostic diagnostic);
            if (diagnostic.Errors.Count > 0)
            {
                string message = string.Join("; ", diagnostic.Errors.Select(e => e.Message));
                throw new OpenApiSpecificationException($"Invalid OpenAPI specification: {message}");
            }

            return document;
        }
        catch (OpenApiSpecificationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new OpenApiSpecificationException($"Invalid OpenAPI specification: {ex.Message}");
        }
    }

    private static string NormalizeIndentation(string content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return content;
        }

        string[] lines = content.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            int tabCount = 0;
            while (tabCount < line.Length && line[tabCount] == '\t')
            {
                tabCount++;
            }

            if (tabCount > 0)
            {
                lines[i] = new string(' ', tabCount * 2) + line[tabCount..];
            }
        }

        return string.Join('\n', lines);
    }
}
