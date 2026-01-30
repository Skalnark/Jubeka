namespace Jubeka.Core.Application;

public interface IQueryParser
{
    IReadOnlyList<(string Key, string Value)> Parse(IEnumerable<string>? rawQueryParameters, IReadOnlyDictionary<string, string> vars);

    List<(string Key, string Value)> ParseQueryParametersFromUrl(string rawUrl);
}