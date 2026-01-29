using Jubeka.Core.Domain;

namespace Jubeka.Core.Application;

public interface IRequestDataBuilder
{
    RequestData Build(RequestOptions options, IReadOnlyDictionary<string, string> vars);
}
