using Jubeka.Core.Domain;
using Microsoft.OpenApi.Models;

namespace Jubeka.Core.Application;

public interface IOpenApiRequestBuilder
{
    RequestOptions Build(OpenApiDocument document, string operationId, IReadOnlyDictionary<string, string> vars);
}
