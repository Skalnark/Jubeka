namespace Jubeka.Core.Application;

public interface IUriBuilderHelper
{
    // TODO: make URI nullable
    Uri Build(string rawUrl, IReadOnlyDictionary<string, string> vars, IReadOnlyList<(string Key, string Value)> queryParams);

    // TODO: add this method to the interface
    //Uri? BuildFromUrl(string rawUrl, IReadOnlyDictionary<string, string> vars);
}
