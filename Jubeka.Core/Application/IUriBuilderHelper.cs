using Jubeka.Core.Domain;

namespace Jubeka.Core.Application;

public interface IUriBuilderHelper
{
    Uri Build(string rawUrl, IReadOnlyDictionary<string, string> vars, IReadOnlyList<(string Key, string Value)> queryParams);

    Uri BuildFromUrl(string rawUrl, IReadOnlyDictionary<string, string> vars);

    RequestComponents BuildUriComponents(string rawUrl);
}
