using Jubeka.CLI.Domain;

namespace Jubeka.CLI.Application;

public interface IEnvironmentWizard
{
    EnvironmentConfig BuildEnvironmentConfig(EnvConfigOptions options, string action);
}
