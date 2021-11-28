using System;
using ImageMagitek.Colors.Converters;
using ImageMagitek.Utility.Parsing;

namespace ImageMagitek.Colors;

public interface IColorFactory
{
    /// <summary>
    /// Creates a new color of the specified ColorModel with a default color value
    /// </summary>
    IColor CreateColor(ColorModel model);

    /// <summary>
    /// Creates a new color of the specified ColorModel with the specified foreign color value
    /// </summary>
    IColor CreateColor(ColorModel model, uint color);

    /// <summary>
    /// Creates a new color of the specified ColorModel with the specified foreign color value components
    /// </summary>
    IColor CreateColor(ColorModel colorModel, int r, int g, int b, int a);

    /// <summary>
    /// Deep clones the supplied color
    /// </summary>
    IColor CloneColor(IColor color);

    /// <summary>
    /// Converts the specified color to the native color type
    /// </summary>
    ColorRgba32 ToNative(IColor color);

    /// <summary>
    /// Converts the specified native color to a foreign color type
    /// </summary>
    IColor ToForeign(ColorRgba32 color, ColorModel colorModel);

    /// <summary>
    /// Tries to create a color from the specified hexadecimal string
    /// </summary>
    bool FromHexString(string hexString, ColorModel colorModel, out IColor color);

    /// <summary>
    /// Converts the specified color to a hex string
    string ToHexString(IColor color);
}

/// <inheritdoc/>
public class ColorFactory : IColorFactory
{
    private readonly ColorConverterBgr15 _bgr15Converter = new ColorConverterBgr15();
    private readonly ColorConverterAbgr16 _abgr16Converter = new ColorConverterAbgr16();
    private ColorConverterNes _nesConverter;
    private readonly ColorConverterBgr9 _bgr9Converter = new ColorConverterBgr9();
    private readonly ColorConverterBgr6 _bgr6Converter = new ColorConverterBgr6();

    public void SetNesPalette(Palette nesPalette)
    {
        _nesConverter = new ColorConverterNes(nesPalette);
    }

    public IColor CreateColor(ColorModel model) => CreateColor(model, 0);

    public IColor CreateColor(ColorModel colorModel, uint color)
    {
        return colorModel switch
        {
            ColorModel.Rgba32 => new ColorRgba32(color),
            ColorModel.Bgr15 => new ColorBgr15(color),
            ColorModel.Abgr16 => new ColorAbgr16(color),
            ColorModel.Nes => new ColorNes(color),
            ColorModel.Bgr9 => new ColorBgr9(color),
            ColorModel.Bgr6 => new ColorBgr6(color),
            _ => throw new NotSupportedException($"{nameof(ColorModel)} '{colorModel}' is not supported")
        };
    }

    public IColor CreateColor(ColorModel colorModel, int r, int g, int b, int a)
    {
        return colorModel switch
        {
            ColorModel.Rgba32 => new ColorRgba32((byte)r, (byte)g, (byte)b, (byte)a),
            ColorModel.Bgr15 => new ColorBgr15((byte)r, (byte)g, (byte)b),
            ColorModel.Abgr16 => new ColorAbgr16((byte)r, (byte)g, (byte)b, (byte)a),
            ColorModel.Nes => _nesConverter.ToForeignColor(new ColorRgba32((byte)r, (byte)g, (byte)b, (byte)a)),
            ColorModel.Bgr9 => new ColorBgr9((byte)r, (byte)g, (byte)b),
            ColorModel.Bgr6 => new ColorBgr6((byte)r, (byte)g, (byte)b),
            _ => throw new NotSupportedException($"{nameof(ColorModel)} '{colorModel}' is not supported")
        };
    }

    public IColor CloneColor(IColor color)
    {
        return color switch
        {
            ColorRgba32 rgba32 => new ColorRgba32(rgba32.Color),
            ColorBgr15 bgr15 => new ColorBgr15(bgr15.R, bgr15.G, bgr15.B),
            ColorAbgr16 abgr16 => new ColorAbgr16(abgr16.R, abgr16.G, abgr16.B, abgr16.A),
            ColorNes nes => new ColorNes(color.Color),
            ColorBgr9 bgr9 => new ColorBgr9(bgr9.R, bgr9.G, bgr9.B),
            ColorBgr6 bgr6 => new ColorBgr6(bgr6.R, bgr6.G, bgr6.B),
            _ => throw new NotSupportedException($"{nameof(IColor)} '{color}' is not supported")
        };
    }

    public ColorRgba32 ToNative(IColor color)
    {
        return color switch
        {
            ColorBgr15 colorBgr15 => _bgr15Converter.ToNativeColor(colorBgr15),
            ColorAbgr16 colorAbgr16 => _abgr16Converter.ToNativeColor(colorAbgr16),
            ColorRgba32 _ => new ColorRgba32(color.Color),
            ColorNes colorNes => _nesConverter?.ToNativeColor(colorNes) ??
                throw new ArgumentException($"{nameof(ToNative)} has no NES color converter defined"),
            ColorBgr9 colorBgr9 => _bgr9Converter.ToNativeColor(colorBgr9),
            ColorBgr6 colorBgr6 => _bgr6Converter.ToNativeColor(colorBgr6),
            _ => throw new NotSupportedException($"{nameof(ToNative)} '{color}' is not supported"),
        };
    }

    public IColor ToForeign(ColorRgba32 color, ColorModel colorModel)
    {
        return colorModel switch
        {
            ColorModel.Rgba32 => new ColorRgba32(color.Color),
            ColorModel.Bgr15 => _bgr15Converter.ToForeignColor(color),
            ColorModel.Abgr16 => _abgr16Converter.ToForeignColor(color),
            ColorModel.Nes => _nesConverter?.ToForeignColor(color) ??
                throw new ArgumentException($"{nameof(ToForeign)} has no NES color converter defined"),
            ColorModel.Bgr9 => _bgr9Converter.ToForeignColor(color),
            ColorModel.Bgr6 => _bgr6Converter.ToForeignColor(color),
            _ => throw new NotSupportedException($"{nameof(ToForeign)} '{colorModel}' is not supported"),
        };
    }

    public bool FromHexString(string hexString, ColorModel colorModel, out IColor color)
    {
        if (ColorParser.TryParse(hexString, colorModel, out var resultColor))
        {
            color = resultColor;
            return true;
        }

        color = default;
        return false;
    }

    public string ToHexString(IColor color)
    {
        return color switch
        {
            ColorRgba32 rgba32 => $"#{rgba32.R:X02}{rgba32.G:X02}{rgba32.B:X02}{rgba32.A:X02}",
            ColorBgr15 bgr15 => $"#{bgr15.Color:X04}",
            ColorAbgr16 abgr15 => $"#{abgr15.Color:X04}",
            ColorNes nes => $"#{nes.Color:X02}",
            ColorBgr9 bgr9 => $"#{bgr9.Color:X04}",
            ColorBgr6 bgr6 => $"#{bgr6.Color:X02}",
            _ => throw new NotSupportedException($"{nameof(ToString)} '{color.GetType()}' is not supported"),
        };
    }
}
