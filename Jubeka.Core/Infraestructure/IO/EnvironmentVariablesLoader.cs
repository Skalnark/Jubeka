using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using Jubeka.Core.Application;

namespace Jubeka.Core.Infrastructure.IO;

public sealed class EnvironmentVariablesLoader : IEnvironmentVariablesLoader
{
    public IReadOnlyDictionary<string, string> Load(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Environment variables file not found.", path);
        }

        string yaml = NormalizeYamlIndentation(File.ReadAllText(path));
        IDeserializer deserializer = new DeserializerBuilder()
            .IgnoreUnmatchedProperties()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();
        EnvironmentYaml? env = deserializer.Deserialize<EnvironmentYaml>(yaml);

        Dictionary<string, string> vars = new(StringComparer.OrdinalIgnoreCase);
        if (env?.Variables != null)
        {
            foreach ((string key, string value) in env.Variables)
            {
                if (!string.IsNullOrWhiteSpace(key))
                {
                    vars[key] = value;
                }
            }
        }

        return vars;
    }

    private sealed class EnvironmentYaml
    {
        public Dictionary<string, string>? Variables { get; init; }
    }

    private static string NormalizeYamlIndentation(string yaml)
    {
        if (string.IsNullOrEmpty(yaml))
        {
            return yaml;
        }

        string[] lines = yaml.Split('\n');
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
