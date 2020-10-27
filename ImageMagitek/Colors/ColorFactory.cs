using System;
using System.Collections.Generic;
using ImageMagitek.Colors.Converters;

namespace ImageMagitek.Colors
{
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
    }

    public class ColorFactory : IColorFactory
    {
        private readonly ColorConverterBgr15 _bgr15Converter = new ColorConverterBgr15();
        private readonly ColorConverterAbgr16 _abgr16Converter = new ColorConverterAbgr16();
        private ColorConverterNes _nesConverter;

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
                _ => throw new NotSupportedException($"{nameof(ToForeign)} '{colorModel}' is not supported"),
            };
        }
    }
}
