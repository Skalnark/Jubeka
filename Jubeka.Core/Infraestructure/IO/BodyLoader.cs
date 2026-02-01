using Jubeka.Core.Application;

namespace Jubeka.Core.Infrastructure.IO;

public sealed class BodyLoader : IBodyLoader
{
    public string Load(string? args)
    {
        if (string.IsNullOrEmpty(args))
        {
            return string.Empty;
        }

        if (args.StartsWith("@", StringComparison.Ordinal))
        {
            string filePath = args[1..];
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("Body file not found.", filePath);
            }

            return File.ReadAllText(filePath);
        }
        return args;
    }
}