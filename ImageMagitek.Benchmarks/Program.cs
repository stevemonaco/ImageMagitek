using BenchmarkDotNet.Running;
using System;

namespace ImageMagitek.Benchmarks
{
    class Program
    {
        static void Main(string[] args)
        {
            BenchmarkRunner.Run(typeof(FileStreamReopenPerRead));
        }
    }
}
