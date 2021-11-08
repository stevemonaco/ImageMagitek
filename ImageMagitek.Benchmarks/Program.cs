using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;
using System;

namespace ImageMagitek.Benchmarks;

class Program
{
    static void Main(string[] args)
    {
        //BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, new DebugInProcessConfig());
        //BenchmarkRunner.Run(typeof(FileStreamReopenPerRead));
        BenchmarkRunner.Run(typeof(Snes3bppDecodeToImage));
    }
}
