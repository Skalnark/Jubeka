using System;
using Jubeka.CLI.Application;

namespace Jubeka.CLI.Infrastructure.Console;

public sealed class ConsolePrompt : IPrompt
{
    public string PromptWithDefault(string label, string? defaultValue)
    {
        string prompt = string.IsNullOrWhiteSpace(defaultValue)
            ? $"{label}: "
            : $"{label} [{defaultValue}]: ";
        System.Console.Write(prompt);
        string? input = System.Console.ReadLine();

        if (string.IsNullOrWhiteSpace(input))
        {
            return defaultValue ?? string.Empty;
        }

        return input.Trim();
    }

    public string? PromptOptional(string label, string? defaultValue)
    {
        string prompt = string.IsNullOrWhiteSpace(defaultValue)
            ? $"{label}: "
            : $"{label} [{defaultValue}]: ";
        System.Console.Write(prompt);
        string? input = System.Console.ReadLine();
        if (string.IsNullOrWhiteSpace(input))
        {
            return defaultValue;
        }

        return input.Trim();
    }

    public string PromptRequired(string label, string? defaultValue)
    {
        while (true)
        {
            string prompt = string.IsNullOrWhiteSpace(defaultValue)
                ? $"{label}: "
                : $"{label} [{defaultValue}]: ";
            System.Console.Write(prompt);
            string? input = System.Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input))
            {
                if (!string.IsNullOrWhiteSpace(defaultValue))
                {
                    return defaultValue;
                }

                System.Console.WriteLine($"{label} is required.");
                continue;
            }

            return input.Trim();
        }
    }

    public bool? PromptYesNo(string label, bool? defaultValue)
    {
        string suffix = defaultValue == true ? "[Y/n]" : defaultValue == false ? "[y/N]" : "[y/n]";
        System.Console.Write($"{label} {suffix}: ");
        string? input = System.Console.ReadLine();

        if (string.IsNullOrWhiteSpace(input))
        {
            return defaultValue;
        }

        return input.Trim().StartsWith("y", StringComparison.OrdinalIgnoreCase);
    }
}
