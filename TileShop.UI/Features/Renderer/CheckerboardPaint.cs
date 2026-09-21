using System;
using SkiaSharp;
using TileShop.UI.Models;

namespace TileShop.UI.Renderer;

/// <summary>
/// A paint tiling a checkerboard at a grid's spacing, origin, and colors, rebuilt only when those change
/// </summary>
public sealed class CheckerboardPaint : IDisposable
{
    private readonly record struct Key(int CellWidth, int CellHeight, int OriginX, int OriginY, SKColor Primary, SKColor Secondary);

    private SKPaint? _paint;
    private Key _key;

    public SKPaint Get(GridSettingsViewModel grid)
    {
        var key = new Key(Math.Max(1, grid.WidthSpacing), Math.Max(1, grid.HeightSpacing),
            grid.OriginX, grid.OriginY, grid.PrimaryColor.ToSKColor(), grid.SecondaryColor.ToSKColor());

        if (_paint is not null && key == _key)
            return _paint;

        _paint?.Dispose();
        _paint = Create(key);
        _key = key;
        return _paint;
    }

    private static SKPaint Create(Key key)
    {
        var bitmap = new SKBitmap(key.CellWidth * 2, key.CellHeight * 2);
        using (var canvas = new SKCanvas(bitmap))
        using (var secondaryPaint = new SKPaint { Color = key.Secondary, BlendMode = SKBlendMode.Src })
        {
            canvas.Clear(key.Primary);
            canvas.DrawRect(0, 0, key.CellWidth, key.CellHeight, secondaryPaint);
            canvas.DrawRect(key.CellWidth, key.CellHeight, key.CellWidth, key.CellHeight, secondaryPaint);
        }

        var origin = SKMatrix.CreateTranslation(key.OriginX, key.OriginY);
        var sampling = new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None);
        var shader = bitmap.ToShader(SKShaderTileMode.Repeat, SKShaderTileMode.Repeat, sampling, origin);
        return new SKPaint { Shader = shader };
    }

    public void Dispose() => _paint?.Dispose();
}

public static class SkiaColorExtensions
{
    public static SKColor ToSKColor(this Avalonia.Media.Color color) => new(color.R, color.G, color.B, color.A);
}
