using System.Net;
using System.Net.Sockets;
using System.Text;
using Jubeka.Core.Domain;
using Jubeka.Core.Infrastructure.Http;
using Xunit;

namespace Jubeka.Core.Tests.Infrastructure.Http;

// NOTE: This test was written by AI, don't trust it
// TODO: Rewrite this test
public class HttpRequestExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_SendsRequestAndReturnsResponse()
    {
        string? receivedHeader = null;
        string? receivedBody = null;

        // Reserve a free port
        var port = GetFreePort();

        TcpListener listener = new(IPAddress.Loopback, port);
        listener.Start();

        Task serverTask = Task.Run(async () =>
        {
            try
            {
                using TcpClient client = await listener.AcceptTcpClientAsync().ConfigureAwait(false);
                using NetworkStream stream = client.GetStream();
                using StreamReader reader = new(stream, Encoding.ASCII, false, 1024, true);
                using StreamWriter writer = new(stream, Encoding.ASCII, 1024, true) { NewLine = "\r\n", AutoFlush = true };

                // Read request headers
                string? line;
                List<string> headers = [];
                while ((line = await reader.ReadLineAsync().ConfigureAwait(false)) is { } && line != string.Empty)
                {
                    headers.Add(line);
                }

                foreach (var h in headers)
                {
                    if (h.StartsWith("X-Test:", StringComparison.OrdinalIgnoreCase))
                    {
                        receivedHeader = h.Substring(h.IndexOf(':') + 1).Trim();
                    }
                }

                // Read body if any
                int contentLength = 0;
                foreach (var h in headers)
                {
                    if (h.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                    {
                        var parts = h.Split(':', 2);
                        if (parts.Length == 2 && int.TryParse(parts[1].Trim(), out var len)) contentLength = len;
                    }
                }

                if (contentLength > 0)
                {
                    var buf = new char[contentLength];
                    int read = 0;
                    while (read < contentLength)
                    {
                        int r = await reader.ReadAsync(buf, read, contentLength - read).ConfigureAwait(false);
                        if (r == 0) break;
                        read += r;
                    }
                    receivedBody = new string(buf, 0, read);
                }

                // Send simple response
                var resp = "HTTP/1.1 200 OK\r\nX-Resp: v1\r\nContent-Type: text/plain; charset=utf-8\r\nContent-Length: 2\r\n\r\nok";
                await writer.WriteAsync(resp).ConfigureAwait(false);
                await writer.FlushAsync().ConfigureAwait(false);
            }
            finally
            {
                try { listener.Stop(); } catch { }
            }
        });

        Uri uri = new UriBuilder("http", "127.0.0.1", port, "/test").Uri;
        List<(string Key, string Value)> headersList = [("X-Test", "v1")];
        RequestData requestData = new(HttpMethod.Post, uri, headersList, "hello-body");

        ResponseData result = await HttpRequestExecutor.ExecuteAsync(requestData, TimeSpan.FromSeconds(5), CancellationToken.None).ConfigureAwait(false);

        // Wait for server task to complete processing
        await serverTask.ConfigureAwait(false);

        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.Equal("ok", result.Body);
        Assert.True(result.IsSuccessStatusCode);

        // Response headers should contain X-Resp
        Assert.Contains(("X-Resp", "v1"), result.Headers);

        // Ensure the request we received by the test server included the header and body
        Assert.Equal("v1", receivedHeader);
        Assert.Equal("hello-body", receivedBody);
    }

    private static int GetFreePort()
    {
        TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
