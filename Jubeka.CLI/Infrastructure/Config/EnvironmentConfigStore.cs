using System;
using System.IO;
using System.Text.Json;
using Jubeka.CLI.Application;
using Jubeka.CLI.Domain;

namespace Jubeka.CLI.Infrastructure.Config;

public sealed class EnvironmentConfigStore : IEnvironmentConfigStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    public EnvironmentConfig? Get(string name)
    {
        string path = GetConfigPath(name);
        if (!File.Exists(path))
        {
            return null;
        }

        string json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<EnvironmentConfig>(json, SerializerOptions);
    }

    public void Save(EnvironmentConfig config)
    {
        string directory = GetConfigDirectory();
        Directory.CreateDirectory(directory);
        string path = GetConfigPath(config.Name);
        string json = JsonSerializer.Serialize(config, SerializerOptions);
        File.WriteAllText(path, json);
    }

    private static string GetConfigDirectory()
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".config", "jubeka");
    }

    private static string GetConfigPath(string name)
    {
        string fileName = $"{name}.json";
        return Path.Combine(GetConfigDirectory(), fileName);
    }
}
