namespace Jubeka.Core.Application;

public interface IHeaderParser
{
    IReadOnlyList<(string Key, string Value)> Parse(IEnumerable<string> rawHeaders, IReadOnlyDictionary<string, string> vars);
}