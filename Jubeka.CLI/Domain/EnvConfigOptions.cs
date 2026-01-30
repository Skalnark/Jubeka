using Jubeka.Core.Domain;

namespace Jubeka.CLI.Domain;

public sealed record EnvConfigOptions(
    string Name,
    string VarsPath,
    OpenApiSource? DefaultOpenApiSource,
    bool Local
);
