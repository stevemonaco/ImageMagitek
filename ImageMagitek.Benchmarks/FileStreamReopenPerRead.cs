using System;
using System.Collections.Generic;
using System.IO;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

namespace ImageMagitek.Benchmarks
{
    /// <summary>
    /// Benchmark to determine performace impact of continually reopening FileStream objects contained by DataFiles
    /// </summary>
    public class FileStreamReopenPerRead
    {
        [Params(16, 64, 256, 512, 16384)]
        public int TotalReadSize;

        private const int SizePerRead = 16;
        private const string fileName = "FileStreamReopenPerReadTestData.bin";

        [GlobalSetup]
        public void GlobalSetup()
        {
            using (var fs = File.Create(fileName))
            {
                Random rng = new Random();
                var data = new byte[16384];
                rng.NextBytes(data);
                fs.Write(data);
            }
        }

        [GlobalCleanup]
        public void GlobalCleanup()
        {
            File.Delete(fileName);
        }

        [Benchmark]
        public void KeepOpen()
        {
            using(var fs = File.OpenRead(fileName))
            using(var br = new BinaryReader(fs))
            {
                for(int i = 0; i < TotalReadSize; i += SizePerRead)
                {
                    fs.Seek(i, SeekOrigin.Begin);
                    br.ReadBytes(SizePerRead);
                }
            }
        }

        [Benchmark]
        public void ReopenPerRead()
        {
            for (int i = 0; i < TotalReadSize; i += SizePerRead)
            {
                using (var fs = File.OpenRead(fileName))
                using (var br = new BinaryReader(fs))
                {
                    fs.Seek(i, SeekOrigin.Begin);
                    br.ReadBytes(SizePerRead);
                }
            }
        }
    }
}
