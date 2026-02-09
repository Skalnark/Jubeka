namespace Jubeka.CLI.Application;

public interface IPrompt
{
    string PromptWithDefault(string label, string? defaultValue);
    string? PromptOptional(string label, string? defaultValue);
    string PromptRequired(string label, string? defaultValue);
    bool? PromptYesNo(string label, bool? defaultValue);
}
