using Jubeka.Core.Infrastructure.IO;
using Xunit;

namespace Jubeka.Core.Tests.Infrastructure.IO
{
    public class BodyLoaderTests
    {
        [Theory]
        [InlineData(null, "")]
        [InlineData("plain", "plain")]
        public void Load_NullOrLiteral_ReturnsExpected(string? arg, string expected)
        {
            BodyLoader loader = new();
            Assert.Equal(expected, loader.Load(arg));
        }

        [Fact]
        public void Load_FileContents_ReturnsFileText()
        {
            string tmp = Path.GetTempFileName();
            try
            {
                File.WriteAllText(tmp, "hello-body");
                BodyLoader loader = new();
                string outp = loader.Load("@" + tmp);
                Assert.Equal("hello-body", outp);
            }
            finally { if (File.Exists(tmp)) File.Delete(tmp); }
        }

        [Fact]
        public void Load_MissingFile_Throws()
        {
            BodyLoader loader = new();
            Assert.Throws<FileNotFoundException>(() => loader.Load("@non-existent-file-xyz"));
        }

        // TODO: test all body types
    }
}
