using Jubeka.CLI.Infrastructure.Console;

namespace Jubeka.CLI.Tests.Infrastructure.Console;

[Collection("ConsoleTests")]
public class HelpPrinterTests
{
    [Fact]
    public void Print_NoError_ReturnsZeroAndWritesUsage()
    {
        HelpPrinter printer = new();
        TextWriter originalOut = System.Console.Out;
        TextWriter originalErr = System.Console.Error;
        using StringWriter output = new();
        using StringWriter error = new();

        try
        {
            System.Console.SetOut(output);
            System.Console.SetError(error);

            int code = printer.Print();

            Assert.Equal(0, code);
            Assert.Contains("Jubeka CLI", output.ToString());
            Assert.Contains("openapi request", output.ToString());
            Assert.Contains("env create", output.ToString());
            Assert.Contains("env set", output.ToString());
            Assert.Contains("env request add", output.ToString());
            Assert.Contains("env request list", output.ToString());
            Assert.Contains("env request edit", output.ToString());
            Assert.Equal(string.Empty, error.ToString());
        }
        finally
        {
            System.Console.SetOut(originalOut);
            System.Console.SetError(originalErr);
        }
    }

    [Fact]
    public void Print_WithError_ReturnsOneAndWritesError()
    {
        HelpPrinter printer = new();
        TextWriter originalOut = System.Console.Out;
        TextWriter originalErr = System.Console.Error;
        using StringWriter output = new();
        using StringWriter error = new();

        try
        {
            System.Console.SetOut(output);
            System.Console.SetError(error);

            int code = printer.Print("bad");

            Assert.Equal(1, code);
            Assert.Contains("bad", error.ToString());
            Assert.Contains("Usage:", output.ToString());
        }
        finally
        {
            System.Console.SetOut(originalOut);
            System.Console.SetError(originalErr);
        }
    }
}
