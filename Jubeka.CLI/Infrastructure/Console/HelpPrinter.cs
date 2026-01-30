using Jubeka.CLI.Application;

namespace Jubeka.CLI.Infrastructure.Console;

public sealed class HelpPrinter : IHelpPrinter
{
    public int Print(string? error = null)
    {
        if (!string.IsNullOrWhiteSpace(error))
        {
            System.Console.Error.WriteLine(error);
            System.Console.Error.WriteLine();
        }

        System.Console.WriteLine("Jubeka CLI - REST client");
        System.Console.WriteLine();
        System.Console.WriteLine("Usage:");
        System.Console.WriteLine("  jubeka request --method <VERB> --url <URL> [options]");
        System.Console.WriteLine();
        System.Console.WriteLine("Options:");
        System.Console.WriteLine("  -m, --method   HTTP method (GET, POST, PUT, PATCH, DELETE, etc.)");
        System.Console.WriteLine("  -u, --url      Request URL (can include existing query)");
        System.Console.WriteLine("  -b, --body     Request body string (use --body @path for file)");
        System.Console.WriteLine("  -q, --query    Query parameter (repeatable, format key=value)");
        System.Console.WriteLine("  -H, --header   Header (repeatable, format Name: Value)");
        System.Console.WriteLine("  -t, --timeout  Timeout in seconds (default: 100)");
        System.Console.WriteLine("  --pretty       Pretty-print JSON response if possible");
        System.Console.WriteLine("  -h, --help     Show this help");
        System.Console.WriteLine();

        return string.IsNullOrWhiteSpace(error) ? 0 : 1;
    }
}
