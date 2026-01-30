using Jubeka.Core.Infrastructure.IO;
using Xunit;

namespace Jubeka.Core.Tests.Infraestructure.IO;

public class EnvironmentVariablesLoaderTests
{
    [Fact]
    public void Load_EmptyPath_ReturnsEmptyDictionary()
    {
        EnvironmentVariablesLoader loader = new();
        IReadOnlyDictionary<string, string> vars = loader.Load(null);
        Assert.Empty(vars);
    }

    [Fact]
    public void Load_ValidYaml_ReturnsVariables()
    {
        string tmp = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tmp, "variables:\n  baseUrl: https://api.example.com\n  token: abc123\n");
            EnvironmentVariablesLoader loader = new();
            IReadOnlyDictionary<string, string> vars = loader.Load(tmp);

            Assert.Equal("https://api.example.com", vars["baseUrl"]);
            Assert.Equal("abc123", vars["token"]);
        }
        finally
        {
            if (File.Exists(tmp)) File.Delete(tmp);
        }
    }

    [Fact]
    public void Load_MissingFile_Throws()
    {
        EnvironmentVariablesLoader loader = new();
        Assert.Throws<FileNotFoundException>(() => loader.Load("/tmp/does-not-exist.yml"));
    }
}
