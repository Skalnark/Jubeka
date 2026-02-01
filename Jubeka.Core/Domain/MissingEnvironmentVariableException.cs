namespace Jubeka.Core.Domain;

public sealed class MissingEnvironmentVariableException : Exception
{
    public MissingEnvironmentVariableException(IReadOnlyList<string> missingVariables)
        : base($"Missing environment variables: {string.Join(", ", missingVariables)}")
    {
        MissingVariables = missingVariables;
    }

    public IReadOnlyList<string> MissingVariables { get; }
}
