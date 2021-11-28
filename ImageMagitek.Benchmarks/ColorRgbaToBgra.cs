using System;
using System.Collections.Generic;
using System.Numerics;
using BenchmarkDotNet.Attributes;
using ImageMagitek.Colors;

namespace ImageMagitek.Benchmarks;

public class ColorRgbaToBgra
{
    private readonly List<ColorRgba32> _colors = new();

    public ColorRgbaToBgra()
    {
        var rng = new Random();

        for (int i = 0; i < 1024; i++)
        {
            _colors.Add(new ColorRgba32((byte)rng.Next(256), (byte)rng.Next(256), (byte)rng.Next(256), (byte)rng.Next(256)));
        }
    }

    [Benchmark(Baseline = true)]
    public void ManualBitMapping()
    {
        foreach (var inputColor in _colors)
        {
            var outputColor = (uint) (inputColor.B | (inputColor.G << 8) | (inputColor.R << 16) | (inputColor.A << 24));
        }
    }

    [Benchmark]
    public void BitOperationMapping()
    {
        foreach (var color in _colors)
        {
            var inputColor = color.Color;
            var outputColor = (inputColor & 0xFF00FF00) | BitOperations.RotateLeft(inputColor & 0xFF00FF, 16);
        }
    }
}
