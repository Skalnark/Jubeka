namespace Jubeka.Core.Application;

public interface IUriBuilderHelper
{
    Uri Build(string rawUrl, IReadOnlyList<(string Key, string Value)> queryParams);
}
