using System;
using Jubeka.CLI.Domain;
using Jubeka.Core.Domain;

namespace Jubeka.CLI.Application.Default;

public sealed class EnvironmentWizard(IPrompt prompt) : IEnvironmentWizard
{
    public EnvironmentConfig BuildEnvironmentConfig(EnvConfigOptions options, string action)
    {
        Console.WriteLine($"Starting env {action} wizard:");

        string name = prompt.PromptRequired("Name", options.Name);
        string varsDefault = string.IsNullOrWhiteSpace(options.VarsPath) ? $"{name}.yml" : options.VarsPath;
        string varsPath = prompt.PromptWithDefault("YAML vars path", varsDefault);

        OpenApiSource? source = options.DefaultOpenApiSource;
        bool? setSpec = prompt.PromptYesNo("Set default OpenAPI spec?", source != null);
        if (setSpec == true)
        {
            string kindInput = prompt.PromptRequired("Spec source (url|file|raw)", source?.Kind.ToString().ToLowerInvariant() ?? string.Empty);
            OpenApiSourceKind kind = kindInput switch
            {
                "url" => OpenApiSourceKind.Url,
                "file" => OpenApiSourceKind.File,
                "raw" => OpenApiSourceKind.Raw,
                _ => throw new OpenApiSpecificationException("Invalid spec source. Use url, file, or raw.")
            };

            string value = prompt.PromptRequired("Spec value", source?.Value ?? string.Empty);
            source = new OpenApiSource(kind, value);
        }

        return new EnvironmentConfig(name, varsPath, source, []);
    }
}
