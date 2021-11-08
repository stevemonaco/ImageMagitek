using ImageMagitek.Colors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ImageMagitek.UnitTests;

public class TestImageGenerator
{
    /*public Image<Rgba32> GenerateImage(int width, int height, ColorModel colorModel)
    {
        var color = ColorFactory.CreateColor(colorModel);

        if(color.Size > 8)
        {
            return GenerateDirectImage(width, height, colorModel);
        }
        else
        {
            return GenerateIndexedImage(width, height, colorModel);
        }
    }

    private Image<Rgba32> GenerateIndexedImage(int width, int height, ColorModel colorModel)
    {
        var image = new Image<Rgba32>(width, height);
        var color = ColorFactory.CreateColor(colorModel);

        var reds = Enumerable.Range(0, color.RedMax + 1);
        var greens = Enumerable.Range(0, color.GreenMax + 1);
        var blues = Enumerable.Range(0, color.BlueMax + 1);
        var alphas = color.AlphaMax > 0 ? Enumerable.Range(0, color.AlphaMax + 1) : new List<int>();

        var colors = 
    }

    private Image<Rgba32> GenerateDirectImage(int width, int height, ColorModel colorModel)
    {
        var color = ColorFactory.CreateColor(colorModel);

        var colors = new List<Rgba32> { new Rgba32(0, 0, 0, 0), }
    }*/
}
