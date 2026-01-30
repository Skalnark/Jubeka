namespace Jubeka.Core.Application.Default;

public static class VariableSubstitutor
{
    public static string Substitute(string? input, IReadOnlyDictionary<string, string> vars)
    {
        if (string.IsNullOrEmpty(input) || vars.Count == 0)
        {
            return input ?? string.Empty;
        }

        string result = input;

        foreach ((string? key, string? value) in vars)
        {
            result = result.Replace($"{{{{{key}}}}}", value, StringComparison.OrdinalIgnoreCase);
        }

        return result;
    }
}