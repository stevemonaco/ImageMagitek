﻿using System;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace ImageMagitek.UnitTests;
public class ImageRgba32Assert
{
    public static void AreEqual(Image<Rgba32> expected, Image<Rgba32> actual)
    {
        Assert.Equal(expected.Width, actual.Width);
        Assert.Equal(expected.Height, actual.Height);

        if (expected.DangerousTryGetSinglePixelMemory(out var expectedMemory) && actual.DangerousTryGetSinglePixelMemory(out var actualMemory))
        {
            Assert.True(expectedMemory.Span.SequenceEqual(actualMemory.Span));
        }
        else
        {
            for (int y = 0; y < expected.Height; y++)
            {
                for (int x = 0; x < expected.Width; x++)
                {
                    Assert.Equal(expected[x, y], actual[x, y]);
                }
            }
        }
    }
}
