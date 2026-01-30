using Jubeka.CLI.Domain;

namespace Jubeka.CLI.Application;

public interface IEnvironmentConfigStore
{
    EnvironmentConfig? Get(string name);
    void Save(EnvironmentConfig config);
}
