using System.Threading;
using System.Threading.Tasks;

namespace Jubeka.CLI;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        Cli cli = Startup.CreateCli();
        return await cli.RunAsync(args, CancellationToken.None).ConfigureAwait(false);
    }
}