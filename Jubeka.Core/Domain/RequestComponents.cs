namespace Jubeka.Core.Domain;

public class RequestComponents(Uri uri, IReadOnlyList<(string Key, string Value)> queryParams)
{
    public Uri Uri { get; set; } = uri;
    public IReadOnlyList<(string Key, string Value)> QueryParams { get; set; } = queryParams;
}