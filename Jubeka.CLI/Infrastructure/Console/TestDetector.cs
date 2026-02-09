using System;
using System.Linq;

namespace Jubeka.CLI.Infrastructure.Console;

internal static class TestDetector
{
    private static readonly bool _isTestRun = DetectTestRun();

    public static bool IsTestRun => _isTestRun;

    private static bool DetectTestRun()
    {
        try
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var a in assemblies)
            {
                var name = a.GetName().Name?.ToLowerInvariant();
                if (string.IsNullOrEmpty(name))
                    continue;

                if (name.Contains("xunit") || name.Contains("nunit") || name.Contains("mstest") || name.Contains("testhost") || name.Contains("vstest"))
                    return true;
            }
        }
        catch
        {
            // best-effort detection; swallow exceptions and assume not a test run
        }

        return false;
    }
}
