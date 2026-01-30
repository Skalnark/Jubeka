using Jubeka.Core.Domain;
using Jubeka.Core.Infrastructure.OpenApi;
using Microsoft.OpenApi.Models;
using Xunit;

namespace Jubeka.Core.Tests.Infraestructure.OpenApi;

public class OpenApiSpecLoaderTests
{
    [Fact]
    public async Task LoadAsync_RawContent_ParsesDocument()
    {
        string raw = """
openapi: 3.0.0
info:
  title: Test
  version: \"1.0\"
paths:
  /ping:
    get:
      operationId: ping
      responses:
        '200':
          description: ok
""";

        OpenApiSpecLoader loader = new();
        OpenApiDocument doc = await loader.LoadAsync(new OpenApiSource(OpenApiSourceKind.Raw, raw), CancellationToken.None);

        Assert.NotNull(doc);
        Assert.True(doc.Paths.ContainsKey("/ping"));
    }

    [Fact]
    public async Task LoadAsync_InvalidContent_Throws()
    {
        OpenApiSpecLoader loader = new();
        await Assert.ThrowsAsync<OpenApiSpecificationException>(() =>
            loader.LoadAsync(new OpenApiSource(OpenApiSourceKind.Raw, "not a spec"), CancellationToken.None));
    }

    [Fact]
    public async Task LoadAsync_TabIndentedRaw_ParsesDocument()
    {
      string raw = """
  openapi: 3.0.0
  info:
  	title: Test
  	version: \"1.0\"
  paths:
  	/ping:
  		get:
  			operationId: ping
  			responses:
  				'200':
  					description: ok
  """;

      OpenApiSpecLoader loader = new();
      OpenApiDocument doc = await loader.LoadAsync(new OpenApiSource(OpenApiSourceKind.Raw, raw), CancellationToken.None);

      Assert.NotNull(doc);
      Assert.True(doc.Paths.ContainsKey("/ping"));
    }
}
