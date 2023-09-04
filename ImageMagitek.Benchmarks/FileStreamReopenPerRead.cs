using System;
using System.IO;
using BenchmarkDotNet.Attributes;

namespace ImageMagitek.Benchmarks;

/// <summary>
/// Benchmark to determine performace impact of continually reopening FileStream objects contained by DataFiles
/// </summary>
public class FileStreamReopenPerRead
{
    [Params(16, 64, 256, 512, 16384)]
    public int TotalReadSize;

    private const int _sizePerRead = 16;
    private const string _fileName = "FileStreamReopenPerReadTestData.bin";

    [GlobalSetup]
    public void GlobalSetup()
    {
        using var fs = File.Create(_fileName);

        Random rng = new Random();
        var data = new byte[16384];
        rng.NextBytes(data);
        fs.Write(data);
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        File.Delete(_fileName);
    }

    [Benchmark]
    public void KeepOpen()
    {
        using var fs = File.OpenRead(_fileName);
        using var br = new BinaryReader(fs);

        for (int i = 0; i < TotalReadSize; i += _sizePerRead)
        {
            fs.Seek(i, SeekOrigin.Begin);
            br.ReadBytes(_sizePerRead);
        }
    }

    [Benchmark]
    public void ReopenPerRead()
    {
        for (int i = 0; i < TotalReadSize; i += _sizePerRead)
        {
            using var fs = File.OpenRead(_fileName);
            using var br = new BinaryReader(fs);

            fs.Seek(i, SeekOrigin.Begin);
            br.ReadBytes(_sizePerRead);
        }
    }
}
