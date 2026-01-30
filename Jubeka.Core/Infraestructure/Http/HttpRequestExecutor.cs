using System.Text;
using Jubeka.Core.Domain;

namespace Jubeka.Core.Infraestructure.Http;

public static class HttpRequestExecutor
{
    public static async Task<ResponseData> ExecuteAsync(RequestData requestData, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using HttpClient httpClient = new() { Timeout = timeout };
        using HttpRequestMessage httpRequest = new(new HttpMethod(requestData.Method.ToString()), requestData.Uri);

        if(!string.IsNullOrWhiteSpace(requestData.Body))
        {
            httpRequest.Content = new StringContent(requestData.Body);
            /* I forgot to implement content headers in RequestData, so this is commented out for now.
            foreach ((string key, string value) in requestData.ContentHeaders)
            {
                httpRequest.Content.Headers.TryAddWithoutValidation(key, value);
            }
            TODO: Implement content headers in RequestData
            */

            httpRequest.Content = new StringContent(requestData.Body, Encoding.UTF8, "application/json");
        }

        foreach ((string key, string value) in requestData.Headers)
        {
            if(!httpRequest.Headers.TryAddWithoutValidation(key, value))
            {
               httpRequest.Content ??= new StringContent(string.Empty);
               httpRequest.Content.Headers.TryAddWithoutValidation(key, value); 
            }
        }

        // NOTE: I don't know how ConfigureAwait(false) would behave in this context, but adding it to be consistent with the recomendations.
        // https://learn.microsoft.com/pt-br/dotnet/api/system.threading.tasks.task.configureawait?view=net-9.0
        using HttpResponseMessage response = await httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
        string responseBody = response.Content != null ? await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false) : string.Empty;

        List<(string, string)> responseHeaders = [.. response.Headers.SelectMany(h => h.Value.Select(v => (h.Key, v)))];
        List<(string, string)> contentHeaders = [];
        if (response.Content != null)
        {
            contentHeaders.AddRange(response.Content.Headers
                .SelectMany(h => h.Value.Select(v => (h.Key, v))));
        }

        // TODO: Measure elapsed time
        return new ResponseData(
            StatusCode: response.StatusCode,
            Headers: responseHeaders,
            Body: responseBody,
            ContentHeaders: contentHeaders,
            IsSuccessStatusCode: response.IsSuccessStatusCode,
            ReasonPhrase: response.ReasonPhrase,
            Elapsed: null
        );
    }
}