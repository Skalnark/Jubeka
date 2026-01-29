namespace Jubeka.Core.Application.Default;

public sealed class QueryParser(IVariableSubstitutor substitutor) : IQueryParser
{
    public IReadOnlyList<(string Key, string Value)> Parse(IEnumerable<string> rawQueryParameters, IReadOnlyDictionary<string, string> vars)
    {
        List<(string Key, string Value)> result = [];

        foreach (string rawParam in rawQueryParameters)
        {
            string substitutedParam = substitutor.Substitute(rawParam, vars);
            int separatorIndex = substitutedParam.IndexOf('=');

            if (separatorIndex >= 0)
            {
                string key = substitutedParam.Substring(0, separatorIndex);
                string value = substitutedParam.Substring(separatorIndex + 1);
                result.Add((key, value));
            }
            else
            {
                result.Add((substitutedParam, string.Empty));
            }
        }

        return result;
    }
}