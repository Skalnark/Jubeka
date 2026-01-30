using Jubeka.Core.Application.Default;
using Jubeka.Core.Domain;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Readers;
using Xunit;

namespace Jubeka.Core.Tests.Application.Default;

public class OpenApiRequestBuilderTests
{
    [Fact]
    public void Build_UsesVarsForPathQueryAndHeaders()
    {
        string raw = """
openapi: 3.0.0
info:
  title: Test
  version: \"1.0\"
servers:
  - url: https://api.example.com
paths:
  /pets/{id}:
    get:
      operationId: getPet
      parameters:
        - name: id
          in: path
          required: true
          schema:
            type: string
        - name: q
          in: query
          required: false
          schema:
            type: string
        - name: X-Token
          in: header
          required: false
          schema:
            type: string
      responses:
        '200':
          description: ok
""";

        OpenApiStringReader reader = new();
        OpenApiDocument doc = reader.Read(raw, out OpenApiDiagnostic _);
        OpenApiRequestBuilder builder = new();

        Dictionary<string, string> vars = new()
        {
            { "id", "99" },
            { "q", "abc" },
            { "X-Token", "secret" }
        };

        RequestOptions options = builder.Build(doc, "getPet", vars);

        Assert.Equal("GET", options.Method);
        Assert.Contains("https://api.example.com/pets/99", options.Url);
        Assert.Contains("q=abc", options.QueryParameters!);
        Assert.Contains("X-Token: secret", options.Headers!);
    }

    [Fact]
    public void Build_MissingRequiredPathParam_Throws()
    {
        string raw = """
openapi: 3.0.0
info:
  title: Test
  version: \"1.0\"
servers:
  - url: https://api.example.com
paths:
  /pets/{id}:
    get:
      operationId: getPet
      parameters:
        - name: id
          in: path
          required: true
          schema:
            type: string
      responses:
        '200':
          description: ok
""";

        OpenApiStringReader reader = new();
        OpenApiDocument doc = reader.Read(raw, out OpenApiDiagnostic _);
        OpenApiRequestBuilder builder = new();

        Assert.Throws<MissingEnvironmentVariableException>(() => builder.Build(doc, "getPet", new Dictionary<string, string>()));
    }
}
