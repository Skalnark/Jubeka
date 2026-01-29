namespace Jubeka.Core.Application.Default;

public sealed class HeaderParser(IVariableSubstitutor substitutor) : IHeaderParser
{
    public IReadOnlyList<(string Key, string Value)> Parse(IEnumerable<string> rawHeaders, IReadOnlyDictionary<string, string> vars)
    {
        List<(string Key, string Value)> headers = [];

        foreach (string rawHeader in rawHeaders)
        {
            int separatorIndex = rawHeader.IndexOf(':');
            if (separatorIndex <= 0)
            {
                // TODO: Log warning about invalid header format
                continue; // Invalid header format, skip
            }

            string key = rawHeader.Substring(0, separatorIndex).Trim();
            string value = rawHeader.Substring(separatorIndex + 1).Trim();

            string substitutedValue = substitutor.Substitute(value, vars);

            headers.Add((key, substitutedValue));
        }

        return headers;
    }
}