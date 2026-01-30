namespace Jubeka.Core.Application;

public interface IEnvironmentVariablesLoader
{
    IReadOnlyDictionary<string, string> Load(string? path);
}
