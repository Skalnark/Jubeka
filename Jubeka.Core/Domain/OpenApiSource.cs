namespace Jubeka.Core.Domain;

public enum OpenApiSourceKind
{
    Url,
    File,
    Raw
}

public sealed record OpenApiSource(OpenApiSourceKind Kind, string Value);
