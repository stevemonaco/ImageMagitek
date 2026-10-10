using System;
using System.IO;
using System.Threading.Tasks;
using ImageMagitek.Services;

namespace TileShop.CLI;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var app = new CliApplication(Console.Out,
            loggerFactory => CliBootstrap.CreateProjectService(BootstrapPaths.FromDirectory(AppContext.BaseDirectory), loggerFactory, Console.Out),
            Path.Combine(AppContext.BaseDirectory, "errorlogCLI.txt"));

        return await app.RunAsync(args);
    }
}
