using Jubeka.Core.Domain;
using Microsoft.OpenApi.Models;

namespace Jubeka.Core.Application;

public interface IOpenApiSpecLoader
{
    Task<OpenApiDocument> LoadAsync(OpenApiSource source, CancellationToken cancellationToken);
}
