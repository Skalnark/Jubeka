using Jubeka.Core.Domain;

namespace Jubeka.CLI.Domain;

public sealed record EnvEditOptions(
    string Name,
    string? VarsPath,
    OpenApiSource? DefaultOpenApiSource,
    bool Inline
);
