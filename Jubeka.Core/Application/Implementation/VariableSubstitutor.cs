namespace Jubeka.Core.Application.Implementation;

public class VariableSubstitutor : IVariableSubstitutor
{
    public string Substitute(string? input, IReadOnlyDictionary<string, string> vars)
    {
        if (string.IsNullOrEmpty(input) || vars.Count == 0)
        {
            return input ?? string.Empty;
        }

        string result = input;

        // TODO: test this
        foreach ((string? key, string? value) in vars)
        {
            result = result.Replace($"{{{{{key}}}}}", value, StringComparison.OrdinalIgnoreCase);
        }

        return result;
    }
}