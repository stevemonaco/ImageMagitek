using BenchmarkDotNet.Running;
using System;

namespace ImageMagitek.Benchmarks
{
    class Program
    {
        static void Main(string[] args)
        {
            //BenchmarkRunner.Run(typeof(FileStreamReopenPerRead));
            BenchmarkRunner.Run(typeof(Snes3bppDecodeToImage));
            //var test = new Snes3bppDecodeToImage();
            //test.GlobalSetupGeneric();
            //test.DecodeGeneric();
            //test.GlobalCleanupGeneric();
            //test.GlobalSetupNative();
            //test.DecodeNative();
            //test.GlobalCleanupNative();
        }
    }
}
