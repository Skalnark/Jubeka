using Jubeka.CLI.Domain;

namespace Jubeka.CLI.Application;

public interface IEnvironmentConfigStore
{
    EnvironmentConfig? Get(string name, string? baseDirectory = null);
    void Save(EnvironmentConfig config, bool local = false, string? baseDirectory = null);
}
