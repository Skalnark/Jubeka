using Jubeka.CLI.Domain;

namespace Jubeka.CLI.Application;

public interface IEnvironmentConfigStore
{
    EnvironmentConfig? Get(string name, string? baseDirectory = null);
    void Save(EnvironmentConfig config, string? baseDirectory = null);
    string? GetCurrent(string? baseDirectory = null);
    void SetCurrent(string name, string? baseDirectory = null);
}
