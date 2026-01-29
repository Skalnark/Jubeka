using System.IO;
using Jubeka.Core.Infraestructure.IO;
using Xunit;

namespace Jubeka.Core.Tests.Infraestructure.IO
{
    public class BodyLoaderTests
    {
        [Theory]
        [InlineData(null, "")]
        [InlineData("plain", "plain")]
        public void Load_NullOrLiteral_ReturnsExpected(string? arg, string expected)
        {
            var loader = new BodyLoader();
            Assert.Equal(expected, loader.Load(arg));
        }

        [Fact]
        public void Load_FileContents_ReturnsFileText()
        {
            string tmp = Path.GetTempFileName();
            try
            {
                File.WriteAllText(tmp, "hello-body");
                var loader = new BodyLoader();
                string outp = loader.Load("@" + tmp);
                Assert.Equal("hello-body", outp);
            }
            finally { if (File.Exists(tmp)) File.Delete(tmp); }
        }

        [Fact]
        public void Load_MissingFile_Throws()
        {
            var loader = new BodyLoader();
            Assert.Throws<FileNotFoundException>(() => loader.Load("@non-existent-file-xyz"));
        }

        // TODO: test all body types
    }
}
