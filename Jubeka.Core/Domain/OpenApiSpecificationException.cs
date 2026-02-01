namespace Jubeka.Core.Domain;

public sealed class OpenApiSpecificationException : Exception
{
    public OpenApiSpecificationException(string message) : base(message)
    {
    }
}
