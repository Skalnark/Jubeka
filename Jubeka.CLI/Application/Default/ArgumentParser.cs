using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Jubeka.CLI.Domain;
using Jubeka.Core.Domain;

namespace Jubeka.CLI.Application.Default;

public sealed class ArgumentParser : IArgumentParser
{
    public ParseResult Parse(string[] args)
    {
        if (args.Length == 0)
        {
            return ParseResult.Help();
        }

        if (IsEnvCommand(args[0]))
        {
            return ParseEnv(args.Skip(1).ToArray());
        }

        if (IsOpenApiCommand(args[0]))
        {
            return ParseOpenApi(args.Skip(1).ToArray());
        }

        List<string> filteredArgs = [.. args];
        if (filteredArgs.Count > 0 && IsRequestCommand(filteredArgs[0]))
        {
            filteredArgs.RemoveAt(0);
        }
        return ParseRequest(filteredArgs);
    }

    private static bool IsRequestCommand(string value) =>
        value.Equals("request", StringComparison.OrdinalIgnoreCase) || value.Equals("req", StringComparison.OrdinalIgnoreCase);

    private static bool IsOpenApiCommand(string value) =>
        value.Equals("openapi", StringComparison.OrdinalIgnoreCase) || value.Equals("spec", StringComparison.OrdinalIgnoreCase);

    private static bool IsEnvCommand(string value) =>
        value.Equals("env", StringComparison.OrdinalIgnoreCase);

    private static ParseResult ParseRequest(IReadOnlyList<string> args)
    {
        string? method = null;
        string? url = null;
        string? body = null;
        string? envPath = null;
        double timeoutSeconds = 100;
        bool pretty = false;
        List<string> rawQueries = [];
        List<string> rawHeaders = [];

        for (int i = 0; i < args.Count; i++)
        {
            string arg = args[i];
            switch (arg)
            {
                case "-h" or "--help":
                    return ParseResult.Help();
                case "-m" or "--method":
                    if (!TryGetValue(args, ref i, out string? methodValue))
                    {
                        return ParseResult.Help($"Missing value for {arg}.");
                    }
                    method = methodValue;
                    break;
                case "-u" or "--url":
                    if (!TryGetValue(args, ref i, out string? urlValue))
                    {
                        return ParseResult.Help($"Missing value for {arg}.");
                    }
                    url = urlValue;
                    break;
                case "-b" or "--body":
                    if (!TryGetValue(args, ref i, out string? bodyValue))
                    {
                        return ParseResult.Help($"Missing value for {arg}.");
                    }
                    body = bodyValue;
                    break;
                case "-q" or "--query":
                    if (!TryGetValue(args, ref i, out string? queryValue))
                    {
                        return ParseResult.Help($"Missing value for {arg}.");
                    }
                    rawQueries.Add(queryValue);
                    break;
                case "-H" or "--header":
                    if (!TryGetValue(args, ref i, out string? headerValue))
                    {
                        return ParseResult.Help($"Missing value for {arg}.");
                    }
                    rawHeaders.Add(headerValue);
                    break;
                case "-e" or "--env":
                    if (!TryGetValue(args, ref i, out string? envValue))
                    {
                        return ParseResult.Help($"Missing value for {arg}.");
                    }
                    envPath = envValue;
                    break;
                case "-t" or "--timeout":
                    if (!TryGetValue(args, ref i, out string? timeoutValue))
                    {
                        return ParseResult.Help($"Missing value for {arg}.");
                    }
                    if (!double.TryParse(timeoutValue, NumberStyles.Float, CultureInfo.InvariantCulture, out timeoutSeconds) || timeoutSeconds <= 0)
                    {
                        return ParseResult.Help("Invalid timeout. Use a positive number of seconds.");
                    }
                    break;
                case "--pretty":
                    pretty = true;
                    break;
                default:
                    return ParseResult.Help($"Unknown argument '{arg}'.");
            }
        }

        if (string.IsNullOrWhiteSpace(method) || string.IsNullOrWhiteSpace(url))
        {
            return ParseResult.Help("Both --method and --url are required.");
        }

        RequestCommandOptions options = new(method, url, body, envPath, timeoutSeconds, pretty, rawQueries, rawHeaders);
        return ParseResult.Success(CliCommand.Request, options);
    }

    private static ParseResult ParseOpenApi(IReadOnlyList<string> args)
    {
        List<string> filteredArgs = [.. args];
        if (filteredArgs.Count > 0 && filteredArgs[0].Equals("request", StringComparison.OrdinalIgnoreCase))
        {
            filteredArgs.RemoveAt(0);
        }

        string? operationId = null;
        OpenApiSource? source = null;
        string? envPath = null;
        string? envName = null;
        double timeoutSeconds = 100;
        bool pretty = false;

        for (int i = 0; i < filteredArgs.Count; i++)
        {
            string arg = filteredArgs[i];
            switch (arg)
            {
                case "-h" or "--help":
                    return ParseResult.Help();
                case "--operation":
                    if (!TryGetValue(filteredArgs, ref i, out string? opValue))
                    {
                        return ParseResult.Help($"Missing value for {arg}.");
                    }
                    operationId = opValue;
                    break;
                case "--spec-url":
                    if (!TryGetValue(filteredArgs, ref i, out string? urlValue))
                    {
                        return ParseResult.Help($"Missing value for {arg}.");
                    }
                    source = new OpenApiSource(OpenApiSourceKind.Url, urlValue);
                    break;
                case "--spec-file":
                    if (!TryGetValue(filteredArgs, ref i, out string? fileValue))
                    {
                        return ParseResult.Help($"Missing value for {arg}.");
                    }
                    source = new OpenApiSource(OpenApiSourceKind.File, fileValue);
                    break;
                case "--spec-raw":
                    if (!TryGetValue(filteredArgs, ref i, out string? rawValue))
                    {
                        return ParseResult.Help($"Missing value for {arg}.");
                    }
                    source = new OpenApiSource(OpenApiSourceKind.Raw, rawValue);
                    break;
                case "-e" or "--env":
                    if (!TryGetValue(filteredArgs, ref i, out string? envValue))
                    {
                        return ParseResult.Help($"Missing value for {arg}.");
                    }
                    envPath = envValue;
                    break;
                case "--env-name":
                    if (!TryGetValue(filteredArgs, ref i, out string? envNameValue))
                    {
                        return ParseResult.Help($"Missing value for {arg}.");
                    }
                    envName = envNameValue;
                    break;
                case "-t" or "--timeout":
                    if (!TryGetValue(filteredArgs, ref i, out string? timeoutValue))
                    {
                        return ParseResult.Help($"Missing value for {arg}.");
                    }
                    if (!double.TryParse(timeoutValue, NumberStyles.Float, CultureInfo.InvariantCulture, out timeoutSeconds) || timeoutSeconds <= 0)
                    {
                        return ParseResult.Help("Invalid timeout. Use a positive number of seconds.");
                    }
                    break;
                case "--pretty":
                    pretty = true;
                    break;
                default:
                    return ParseResult.Help($"Unknown argument '{arg}'.");
            }
        }

        if (string.IsNullOrWhiteSpace(operationId))
        {
            return ParseResult.Help("--operation is required.");
        }

        OpenApiCommandOptions options = new(operationId, source, envPath, envName, timeoutSeconds, pretty);
        return ParseResult.Success(CliCommand.OpenApiRequest, options);
    }

    private static ParseResult ParseEnv(IReadOnlyList<string> args)
    {
        if (args.Count == 0)
        {
            return ParseResult.Help("env command requires create or update.");
        }

        string action = args[0];
        CliCommand command = action.Equals("create", StringComparison.OrdinalIgnoreCase)
            ? CliCommand.EnvCreate
            : action.Equals("update", StringComparison.OrdinalIgnoreCase) ? CliCommand.EnvUpdate : 0;

        if (command == 0)
        {
            return ParseResult.Help("env command requires create or update.");
        }

        string? name = null;
        string? varsPath = null;
        OpenApiSource? source = null;

        for (int i = 1; i < args.Count; i++)
        {
            string arg = args[i];
            switch (arg)
            {
                case "-h" or "--help":
                    return ParseResult.Help();
                case "--name":
                    if (!TryGetValue(args, ref i, out string? nameValue))
                    {
                        return ParseResult.Help($"Missing value for {arg}.");
                    }
                    name = nameValue;
                    break;
                case "--vars":
                    if (!TryGetValue(args, ref i, out string? varsValue))
                    {
                        return ParseResult.Help($"Missing value for {arg}.");
                    }
                    varsPath = varsValue;
                    break;
                case "--spec-url":
                    if (!TryGetValue(args, ref i, out string? urlValue))
                    {
                        return ParseResult.Help($"Missing value for {arg}.");
                    }
                    source = new OpenApiSource(OpenApiSourceKind.Url, urlValue);
                    break;
                case "--spec-file":
                    if (!TryGetValue(args, ref i, out string? fileValue))
                    {
                        return ParseResult.Help($"Missing value for {arg}.");
                    }
                    source = new OpenApiSource(OpenApiSourceKind.File, fileValue);
                    break;
                case "--spec-raw":
                    if (!TryGetValue(args, ref i, out string? rawValue))
                    {
                        return ParseResult.Help($"Missing value for {arg}.");
                    }
                    source = new OpenApiSource(OpenApiSourceKind.Raw, rawValue);
                    break;
                default:
                    return ParseResult.Help($"Unknown argument '{arg}'.");
            }
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return ParseResult.Help("--name is required.");
        }

        if (string.IsNullOrWhiteSpace(varsPath))
        {
            return ParseResult.Help("--vars is required.");
        }

        EnvConfigOptions options = new(name, varsPath, source);
        return ParseResult.Success(command, options);
    }

    private static bool TryGetValue(IReadOnlyList<string> args, ref int index, out string value)
    {
        if (index + 1 >= args.Count)
        {
            value = string.Empty;
            return false;
        }

        index++;
        value = args[index];
        return true;
    }
}
