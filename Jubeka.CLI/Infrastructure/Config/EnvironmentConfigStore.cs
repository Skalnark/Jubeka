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

    public EnvironmentConfig? Get(string name, string? baseDirectory = null)
    {
        if (!string.IsNullOrWhiteSpace(baseDirectory))
        {
            string localPath = GetLocalConfigPath(baseDirectory, name);
            if (File.Exists(localPath))
            {
                string localJson = File.ReadAllText(localPath);
                return JsonSerializer.Deserialize<EnvironmentConfig>(localJson, SerializerOptions);
            }
        }

        string path = GetGlobalConfigPath(name);
        if (!File.Exists(path))
        {
            return null;
        }

        string json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<EnvironmentConfig>(json, SerializerOptions);
    }

    public void Save(EnvironmentConfig config, bool local = false, string? baseDirectory = null)
    {
        string directory = local
            ? GetLocalConfigDirectory(baseDirectory ?? Directory.GetCurrentDirectory())
            : GetGlobalConfigDirectory();
        Directory.CreateDirectory(directory);
        string path = local
            ? GetLocalConfigPath(baseDirectory ?? Directory.GetCurrentDirectory(), config.Name)
            : GetGlobalConfigPath(config.Name);
        string json = JsonSerializer.Serialize(config, SerializerOptions);
        File.WriteAllText(path, json);
    }

    private static string GetGlobalConfigDirectory()
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".config", "jubeka");
    }

    private static string GetGlobalConfigPath(string name)
    {
        string fileName = $"{name}.json";
        return Path.Combine(GetGlobalConfigDirectory(), fileName);
    }

    private static string GetLocalConfigDirectory(string baseDirectory)
    {
        return Path.Combine(baseDirectory, ".jubeka");
    }

    private static string GetLocalConfigPath(string baseDirectory, string name)
    {
        string fileName = $"{name}.json";
        return Path.Combine(GetLocalConfigDirectory(baseDirectory), fileName);
    }
}
