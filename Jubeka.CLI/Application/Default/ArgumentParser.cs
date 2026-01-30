using System;
using System.Collections.Generic;
using System.Globalization;
using Jubeka.CLI.Domain;

namespace Jubeka.CLI.Application.Default;

public sealed class ArgumentParser : IArgumentParser
{
    public ParseResult Parse(string[] args)
    {
        if (args.Length == 0)
        {
            return ParseResult.Help();
        }

        List<string> filteredArgs = [.. args];
        if (filteredArgs.Count > 0 && IsRequestCommand(filteredArgs[0]))
        {
            filteredArgs.RemoveAt(0);
        }

        string? method = null;
        string? url = null;
        string? body = null;
        string? envPath = null;
        double timeoutSeconds = 100;
        bool pretty = false;
        List<string> rawQueries = new();
        List<string> rawHeaders = new();

        for (int i = 0; i < filteredArgs.Count; i++)
        {
            string arg = filteredArgs[i];
            switch (arg)
            {
                case "-h" or "--help":
                    return ParseResult.Help();
                case "-m" or "--method":
                    if (!TryGetValue(filteredArgs, ref i, out string? methodValue))
                    {
                        return ParseResult.Help($"Missing value for {arg}.");
                    }
                    method = methodValue;
                    break;
                case "-u" or "--url":
                    if (!TryGetValue(filteredArgs, ref i, out string? urlValue))
                    {
                        return ParseResult.Help($"Missing value for {arg}.");
                    }
                    url = urlValue;
                    break;
                case "-b" or "--body":
                    if (!TryGetValue(filteredArgs, ref i, out string? bodyValue))
                    {
                        return ParseResult.Help($"Missing value for {arg}.");
                    }
                    body = bodyValue;
                    break;
                case "-q" or "--query":
                    if (!TryGetValue(filteredArgs, ref i, out string? queryValue))
                    {
                        return ParseResult.Help($"Missing value for {arg}.");
                    }
                    rawQueries.Add(queryValue);
                    break;
                case "-H" or "--header":
                    if (!TryGetValue(filteredArgs, ref i, out string? headerValue))
                    {
                        return ParseResult.Help($"Missing value for {arg}.");
                    }
                    rawHeaders.Add(headerValue);
                    break;
                case "-e" or "--env":
                    if (!TryGetValue(filteredArgs, ref i, out string? envValue))
                    {
                        return ParseResult.Help($"Missing value for {arg}.");
                    }
                    envPath = envValue;
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

        if (string.IsNullOrWhiteSpace(method) || string.IsNullOrWhiteSpace(url))
        {
            return ParseResult.Help("Both --method and --url are required.");
        }

        CLIOptions options = new(method, url, body, envPath, timeoutSeconds, pretty, rawQueries, rawHeaders);
        return ParseResult.Success(options);
    }

    private static bool IsRequestCommand(string value) =>
        value.Equals("request", StringComparison.OrdinalIgnoreCase) || value.Equals("req", StringComparison.OrdinalIgnoreCase);

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
