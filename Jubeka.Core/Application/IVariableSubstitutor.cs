namespace Jubeka.Core.Application;

public interface IVariableSubstitutor
{
    string Substitute(string? input, IReadOnlyDictionary<string, string> vars);
}