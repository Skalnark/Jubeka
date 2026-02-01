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
        System.Console.WriteLine("  jubeka request add --name <ENV_NAME> [options]");
        System.Console.WriteLine("  jubeka request list --name <ENV_NAME> [options]");
        System.Console.WriteLine("  jubeka request edit --name <ENV_NAME> [options]");
        System.Console.WriteLine("  jubeka request exec --name <ENV_NAME> --req-name <REQUEST_NAME> [options]");
        System.Console.WriteLine("  jubeka openapi request --operation <OPERATION_ID> [options]");
        System.Console.WriteLine("  jubeka env create --name <NAME> --vars <PATH> [options]");
        System.Console.WriteLine("  jubeka env update --name <NAME> --vars <PATH> [options]");
        System.Console.WriteLine("  jubeka env edit --name <NAME> [options]");
        System.Console.WriteLine("  jubeka env set --name <NAME> [options]");
        System.Console.WriteLine();
        System.Console.WriteLine("Options:");
        System.Console.WriteLine("  -m, --method   HTTP method (GET, POST, PUT, PATCH, DELETE, etc.)");
        System.Console.WriteLine("  -u, --url      Request URL (can include existing query)");
        System.Console.WriteLine("  -b, --body     Request body string (use --body @path for file)");
        System.Console.WriteLine("  -q, --query    Query parameter (repeatable, format key=value)");
        System.Console.WriteLine("  -H, --header   Header (repeatable, format Name: Value)");
        System.Console.WriteLine("  -e, --env      YAML file with variables for substitution");
        System.Console.WriteLine("  -t, --timeout  Timeout in seconds (default: 100)");
        System.Console.WriteLine("  --pretty       Pretty-print JSON response if possible");
        System.Console.WriteLine("  --spec-url     OpenAPI spec URL");
        System.Console.WriteLine("  --spec-file    OpenAPI spec file path");
        System.Console.WriteLine("  --spec-raw     OpenAPI spec raw string");
        System.Console.WriteLine("  --operation    OpenAPI operationId to invoke");
        System.Console.WriteLine("  --env-name     Use named environment config");
        System.Console.WriteLine("  --name         Environment config name");
        System.Console.WriteLine("  --vars         YAML vars path for environment config");
        System.Console.WriteLine("  --req-name     Request name (collection)");
        System.Console.WriteLine("  --method       Request method (collection)");
        System.Console.WriteLine("  --url          Request URL (collection)");
        System.Console.WriteLine("  --body         Request body (collection)");
        System.Console.WriteLine("  --query        Request query (collection, repeatable)");
        System.Console.WriteLine("  --header       Request header (collection, repeatable)");
        System.Console.WriteLine("  --inline       Update without wizard (request edit, env edit)");
        System.Console.WriteLine("  -h, --help     Show this help");
        System.Console.WriteLine();

        return string.IsNullOrWhiteSpace(error) ? 0 : 1;
    }
}
