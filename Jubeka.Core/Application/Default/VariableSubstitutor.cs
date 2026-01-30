using System.Text.RegularExpressions;
using Jubeka.Core.Domain;

namespace Jubeka.Core.Application.Default;

public static partial class VariableSubstitutor
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
            result = result.Replace($"${{{key}}}", value, StringComparison.OrdinalIgnoreCase);
        }

        return result;
    }

    public static string SubstituteOrThrow(string? input, IReadOnlyDictionary<string, string> vars)
    {
        string result = Substitute(input, vars);
        List<string> missing = GetMissingVariables(result, vars);
        if (missing.Count > 0)
        {
            throw new MissingEnvironmentVariableException(missing);
        }

        return result;
    }

    public static List<string> GetMissingVariables(string? input, IReadOnlyDictionary<string, string> vars)
    {
        if (string.IsNullOrEmpty(input))
        {
            return [];
        }

        HashSet<string> missing = new(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in PlaceholderRegex().Matches(input))
        {
            string key = match.Groups["key"].Value.Trim();
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            if (!vars.ContainsKey(key))
            {
                missing.Add(key);
            }
        }

        return [.. missing];
    }

    [GeneratedRegex(@"\{\{(?<key>[^}]+)\}\}|\$\{(?<key>[^}]+)\}", RegexOptions.Compiled)]
    private static partial Regex PlaceholderRegex();
}