namespace Jubeka.Core.Application;

public interface IUriBuilderHelper
{
    Uri Build(string rawUrl, IReadOnlyDictionary<string, string> vars, IReadOnlyList<(string Key, string Value)> queryParams);
}
