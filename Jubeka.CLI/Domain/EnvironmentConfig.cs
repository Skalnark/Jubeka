using Jubeka.Core.Domain;

namespace Jubeka.CLI.Domain;

public sealed record EnvironmentConfig(
    string Name,
    string VarsPath,
    OpenApiSource? DefaultOpenApiSource
);
