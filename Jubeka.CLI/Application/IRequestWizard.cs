using System.Collections.Generic;
using Jubeka.CLI.Domain;
using Jubeka.Core.Domain;

namespace Jubeka.CLI.Application;

public interface IRequestWizard
{
    RequestDefinition BuildRequest(EnvRequestAddOptions options, IReadOnlyDictionary<string, string> vars);
    RequestDefinition EditRequest(RequestDefinition request, IReadOnlyDictionary<string, string> vars);
    int SelectRequestIndex(IReadOnlyList<RequestDefinition> requests, string? requestName);
}
