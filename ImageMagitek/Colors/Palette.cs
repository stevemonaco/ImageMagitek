using System;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;
using ColorMine.ColorSpaces.Comparisons;
using ImageMagitek.Project;
using ImageMagitek.ExtensionMethods;
using ImageMagitek.Colors.Serialization;

namespace ImageMagitek.Colors
{
    /// <summary>
    /// Determines how strictly colors should be matched to the palette
    /// </summary>
    public enum ColorMatchStrategy { Exact, Nearest }

    //public enum ColorModel { RGBA32 = 0, RGB24, ARGB32, BGR15, ABGR16, RGB15, NES }
    public enum ColorModel { Rgba32 = 0, Bgr15 = 3, Abgr16 = 4, Nes = 6 }

    /// <summary>
    /// Storage source of the palette
    /// Project has IColorSources within DataFile and/or the palette XML project file
    /// Json are for predefined, global palettes
    /// </summary>
    public enum PaletteStorageSource { Project = 0, Json }

    /// <summary>
    /// Palette manages the loading of palettes and colors from a variety of color formats
    /// Local colors are internally ARGB32
    /// Foreign colors are the same as the target system
    /// </summary>
    public class Palette : IProjectResource
    {
        private static readonly Cie94Comparison _comparator = new Cie94Comparison(Cie94Comparison.Application.GraphicArts);
        private readonly IColorFactory _colorFactory;
        private readonly IColorSourceSerializer _colorSerializer;

        public string Name { get; set; }
        public bool CanContainChildResources => false;
        public bool ShouldBeSerialized { get; set; } = true;

        /// <summary>
        /// ColorModel of the palette
        /// </summary>
        public ColorModel ColorModel { get; private set; }

        /// <summary>
        /// DataFile which contains the palette
        /// </summary>
        public DataFile DataFile { get; set; }

        /// <summary>
        /// Number of color entries in the palette
        /// </summary>
        public int Entries { get => ColorSources?.Length ?? 0; }

        /// <summary>
        /// Specifies if the Palette has an alpha channel
        /// </summary>
        public bool HasAlpha { get; private set; }

        /// <summary>
        /// Specifies if the palette's 0-index is automatically treated as transparent
        /// </summary>
        public bool ZeroIndexTransparent { get; set; }

        /// <summary>
        /// Specifies the palette's storage source
        /// </summary>
        public PaletteStorageSource StorageSource { get; private set; }

        /// <summary>
        /// Specifies how the Palette colors will be serialized
        /// </summary>
        public IColorSource[] ColorSources { get; private set; }

        /// <summary>
        /// Gets the internal palette containing native Rgba32 colors
        /// </summary>
        ColorRgba32[] NativePalette { get => _nativePalette.Value; }
        Lazy<ColorRgba32[]> _nativePalette;

        /// <summary>
        /// Gets the internal palette containing foreign colors
        /// </summary>
        IColor[] ForeignPalette { get => _foreignPalette.Value; }
        Lazy<IColor[]> _foreignPalette;

        /// <summary>
        /// Constructs a new named Palette object
        /// </summary>
        /// <param name="PaletteName">Name of the palette</param>
        public Palette(string PaletteName, IColorFactory colorFactory)
        {
            Name = PaletteName;
            _colorFactory = colorFactory;
            _colorSerializer = new ColorSourceSerializer(_colorFactory);

            ColorSources = Array.Empty<IColorSource>();
            HasAlpha = false;
            ZeroIndexTransparent = true;
        }

        public Palette(string name, IColorFactory colorFactory, ColorModel colorModel,
            bool zeroIndexTransparent, PaletteStorageSource storageSource)
        {
            Name = name;
            _colorFactory = colorFactory;
            _colorSerializer = new ColorSourceSerializer(_colorFactory);

            ColorSources = Array.Empty<IColorSource>();
            ColorModel = colorModel;
            ZeroIndexTransparent = zeroIndexTransparent;
            StorageSource = storageSource;

            if (storageSource == PaletteStorageSource.Project)
            {
                _nativePalette = new Lazy<ColorRgba32[]>(() => LoadNativePalette());
                _foreignPalette = new Lazy<IColor[]>(() => LoadForeignPalette());
            }
            else if (storageSource == PaletteStorageSource.Json)
            {
                _nativePalette = new Lazy<ColorRgba32[]>(() => new ColorRgba32[Entries]);
                _foreignPalette = new Lazy<IColor[]>(() => new IColor[Entries]);
            }
        }

        public Palette(string name, IColorFactory colorFactory, ColorModel colorModel, IList<IColorSource> colorSources,
            bool zeroIndexTransparent, PaletteStorageSource storageSource)
        {
            Name = name;
            _colorFactory = colorFactory;
            _colorSerializer = new ColorSourceSerializer(_colorFactory);

            ColorModel = colorModel;
            ColorSources = colorSources.ToArray();
            ZeroIndexTransparent = zeroIndexTransparent;
            StorageSource = storageSource;

            if (storageSource == PaletteStorageSource.Project)
            {
                _nativePalette = new Lazy<ColorRgba32[]>(() => LoadNativePalette());
                _foreignPalette = new Lazy<IColor[]>(() => LoadForeignPalette());
            }
            else if (storageSource == PaletteStorageSource.Json)
            {
                _nativePalette = new Lazy<ColorRgba32[]>(() => new ColorRgba32[Entries]);
                _foreignPalette = new Lazy<IColor[]>(() => new IColor[Entries]);
            }
        }

        /// <summary>
        /// Reloads the palette data from its underlying source
        /// </summary>
        /// <returns></returns>
        public bool Reload()
        {
            return LazyLoadPalette(DataFile, ColorModel, ZeroIndexTransparent);
        }

        /// <summary>
        /// Lazily loads palette from a previously opened file. Actual loading will occur during color access.
        /// </summary>
        /// <param name="dataFile">DataFile containing the palette data</param>
        /// <param name="model">ColorModel of the palette</param>
        /// <param name="zeroIndexTransparent">If the 0-index of the palette is automatically transparent</param>
        /// <returns>Success value</returns>
        public bool LazyLoadPalette(DataFile dataFile, ColorModel model, bool zeroIndexTransparent)
        {
            DataFile = dataFile;
            ColorModel = model;
            ZeroIndexTransparent = zeroIndexTransparent;
            StorageSource = PaletteStorageSource.Project;

            _nativePalette = new Lazy<ColorRgba32[]>(() => LoadNativePalette());
            _foreignPalette = new Lazy<IColor[]>(() => LoadForeignPalette());

            return true;
        }

        private ColorRgba32[] LoadNativePalette()
        {
            var nativePalette = new ColorRgba32[Entries];
            for (int i = 0; i < Entries; i++)
                nativePalette[i] = _colorFactory.ToNative(ForeignPalette[i]); // Will load ForeignPalette if not already loaded

            if (ZeroIndexTransparent)
                nativePalette[0].Color &= 0x00FFFFFF;

            return nativePalette;
        }

        /// <summary>
        /// Loads the ForeignPalette from current settings
        /// </summary>
        /// <exception cref="NotSupportedException">
        /// An unsupported palette format was attempted to be read
        /// or
        /// Palette formats with entry sizes larger than 4 bytes are not supported
        /// </exception>
        private IColor[] LoadForeignPalette()
        {
            //return _paletteSerializer.ReadPalette(DataFile, FileAddress, ColorModel, Entries);
            return _colorSerializer.LoadColors(ColorSources, DataFile, ColorModel, Entries);
        }

        /// <summary>
        /// Returns the native color at the specified index
        /// </summary>
        /// <param name="index">Zero-based palette index</param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public ColorRgba32 this[int index]
        {
            get
            {
                if (NativePalette is null)
                    throw new ArgumentNullException($"{nameof(Palette)}[] property '{nameof(NativePalette)}' was null");

                return NativePalette[index];
            }
        }

        /// <summary>
        /// Gets the color of the native.
        /// </summary>
        /// <param name="index">Zero-based palette index</param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public ColorRgba32 GetNativeColor(int index)
        {
            if (NativePalette is null)
                throw new ArgumentNullException($"{nameof(GetNativeColor)} property '{nameof(NativePalette)}' was null");

            return NativePalette[index];
        }

        public IColor GetForeignColor(int index)
        {
            if (ForeignPalette is null)
                throw new ArgumentNullException($"{nameof(GetForeignColor)} property '{nameof(ForeignPalette)}' was null");

            return ForeignPalette[index];
        }

        /// <summary>
        /// Returns the color at the specified index
        /// </summary>
        /// <param name="index">Zero-based palette index</param>
        /// <returns>Color</returns>
        public Color GetColor(int index)
        {
            if (NativePalette is null)
                throw new ArgumentNullException($"{nameof(GetColor)} property '{nameof(NativePalette)}' was null");

            return Color.FromArgb((int)NativePalette[index].Color);
        }

        public bool ContainsNativeColor(ColorRgba32 color) =>
            NativePalette.Contains(color);

        public bool TryGetIndexByNativeColor(ColorRgba32 color, ColorMatchStrategy matchStrategy, out byte index)
        {
            if (matchStrategy == ColorMatchStrategy.Exact)
            {
                var searchIndex = Array.IndexOf(NativePalette, color);

                if (searchIndex >= 0)
                {
                    index = (byte)searchIndex;
                    return true;
                }
                else
                {
                    index = default;
                    return false;
                }
            }
            else if (matchStrategy == ColorMatchStrategy.Nearest)
            {
                // Try exact match first
                var searchIndex = Array.IndexOf(NativePalette, color);

                if (searchIndex >= 0)
                {
                    index = (byte)searchIndex;
                    return true;
                }

                // Fallback to color comparison

                var c1 = new ColorMine.ColorSpaces.Rgb { R = color.R, G = color.G, B = color.B };
                var h1 = c1.To<ColorMine.ColorSpaces.Hsl>();

                double MinDistance = double.MaxValue;
                byte MinIndex = 0;

                for (byte i = 0; i < Entries; i++)
                {
                    var palColor = NativePalette[i];
                    var c2 = new ColorMine.ColorSpaces.Rgb { R = palColor.R, G = palColor.G, B = palColor.B };
                    var h2 = c2.To<ColorMine.ColorSpaces.Hsl>();

                    double Distance = h1.Compare(h2, _comparator);

                    if (Distance < MinDistance)
                    {
                        MinDistance = Distance;
                        MinIndex = i;
                    }
                }

                index = MinIndex;
                return true;
            }

            throw new NotImplementedException($"{nameof(TryGetIndexByNativeColor)} was called with unknown {nameof(ColorMatchStrategy)}");

            // Color matching involves converting colors to hue-saturation-luminance and comparing

            //var c1 = new ColorMine.ColorSpaces.Rgb { R = color.R(), G = color.G(), B = color.B() };
            //var h1 = c1.To<ColorMine.ColorSpaces.Hsl>();

            //double MinDistance = double.MaxValue;
            //byte MinIndex = 0;
            //Cie94Comparison comparator = new Cie94Comparison(Cie94Comparison.Application.GraphicArts);

            //for(byte i = 0; i < Entries; i++)
            //{
            //    var c2 = new ColorMine.ColorSpaces.Rgb { R = NativePalette[i].R(), G = NativePalette[i].G(), B = NativePalette[i].B() };
            //    var h2 = c2.To<ColorMine.ColorSpaces.Hsl>();

            //    double Distance = c1.Compare(c2, comparator);

            //    if(Distance < MinDistance)
            //    {
            //        MinDistance = Distance;
            //        MinIndex = i;
            //    }
            //}

            //return MinIndex;
        }

        /// <summary>
        /// Returns a palette index matching the specified Native ARGB32 color
        /// </summary>
        /// <param name="color">NativeColor to search for</param>
        /// <param name="exactColorOnly">true to return only exactly matched colors; false to match the closest color</param>
        /// <returns>A palette index matching the specified color</returns>
        public byte GetIndexByNativeColor(ColorRgba32 color, ColorMatchStrategy matchStrategy)
        {
            if (matchStrategy == ColorMatchStrategy.Exact)
            {
                for (byte i = 0; i < Entries; i++)
                {
                    if (NativePalette[i].Color == color.Color)
                        return i;
                }

                // Failed to find the exact color in the palette
                throw new Exception($"{nameof(GetIndexByNativeColor)} could not match exact color");
            }
            else if (matchStrategy == ColorMatchStrategy.Nearest)
            {

            }
            // Color matching involves converting colors to hue-saturation-luminance and comparing
            throw new NotImplementedException();

            //var c1 = new ColorMine.ColorSpaces.Rgb { R = color.R(), G = color.G(), B = color.B() };
            //var h1 = c1.To<ColorMine.ColorSpaces.Hsl>();

            //double MinDistance = double.MaxValue;
            //byte MinIndex = 0;
            //Cie94Comparison comparator = new Cie94Comparison(Cie94Comparison.Application.GraphicArts);

            //for(byte i = 0; i < Entries; i++)
            //{
            //    var c2 = new ColorMine.ColorSpaces.Rgb { R = NativePalette[i].R(), G = NativePalette[i].G(), B = NativePalette[i].B() };
            //    var h2 = c2.To<ColorMine.ColorSpaces.Hsl>();

            //    double Distance = c1.Compare(c2, comparator);

            //    if(Distance < MinDistance)
            //    {
            //        MinDistance = Distance;
            //        MinIndex = i;
            //    }
            //}

            //return MinIndex;
        }

        /// <summary>
        /// Replaces the color at the specified palette index with the specified foreign color
        /// Additionally, updates the native color in the palette
        /// </summary>
        /// <param name="index">Zero-based palette index</param>
        /// <param name="foreignColor">Color to assign to the foreign palette</param>
        public void SetForeignColor(int index, IColor foreignColor)
        {
            if (ForeignPalette is null)
                throw new NullReferenceException($"{nameof(SetForeignColor)} property '{nameof(ForeignPalette)}' was null");

            if (index >= Entries)
                throw new ArgumentOutOfRangeException($"{nameof(GetForeignColor)} parameter '{nameof(index)}' was out of range");

            ForeignPalette[index] = foreignColor;
            NativePalette[index] = _colorFactory.ToNative(foreignColor);
        }

        /// <summary>
        /// Replaces the color at the specified palette index with the specified foreign color
        /// Additionally, updates the native color in the palette
        /// </summary>
        /// <param name="index">Zero-based palette index</param>
        public void SetForeignColor(int index, byte R, byte G, byte B, byte A)
        {
            var fc = _colorFactory.CreateColor(ColorModel, R, G, B, A);

            SetForeignColor(index, fc);
        }

        /// <summary>
        /// Replaces the color at the specified palette index with the specified native color
        /// Additionally, updates the foreign color in the palette
        /// </summary>
        /// <param name="index">Zero-based palette index</param>
        /// <param name="nativeColor">Color to assign to the native palette</param>
        public void SetNativeColor(int index, ColorRgba32 nativeColor)
        {
            if (NativePalette is null)
                throw new NullReferenceException($"{nameof(SetNativeColor)} property '{nameof(NativePalette)}' was null");

            if (index >= Entries)
                throw new ArgumentOutOfRangeException($"{nameof(GetNativeColor)} parameter '{nameof(index)}' was out of range");

            NativePalette[index] = nativeColor;
            ForeignPalette[index] = _colorFactory.ToForeign(nativeColor, ColorModel);
        }

        /// <summary>
        /// Replaces the color at the specified palette index with the specified native color
        /// Additionally, updates the foreign color in the palette
        /// </summary>
        /// <param name="index">Zero-based palette index</param>
        public void SetNativeColor(int index, byte R, byte G, byte B, byte A)
        {
            var nc = _colorFactory.CreateColor(ColorModel.Rgba32, R, G, B, A);

            SetForeignColor(index, nc);
        }

        /// <summary>
        /// Saves palette's foreign colors to its underlying source and location
        /// </summary>
        /// <returns>Success value</returns>
        public bool SavePalette()
        {
            if (StorageSource == PaletteStorageSource.Project)
                _colorSerializer.StoreColors(ColorSources, DataFile, NativePalette, ForeignPalette);

            return true;
        }

        public void SetColorSources(IEnumerable<IColorSource> colorSources)
        {
            ColorSources = colorSources.ToArray();
            Reload();
        }

        /// <summary>
        /// Gets the string name associated with a ColorModel object
        /// </summary>
        /// <param name="ColorModelName">Name of the ColorModel to retrieve</param>
        /// <returns>A string name describing the ColorModel</returns>
        public static ColorModel StringToColorModel(string ColorModelName)
        {
            switch(ColorModelName)
            {
                case "Rgba32":
                    return ColorModel.Rgba32;
                case "Bgr15":
                    return ColorModel.Bgr15;
                case "Abgr16":
                    return ColorModel.Abgr16;
                case "Nes":
                    return ColorModel.Nes;
                default:
                    throw new ArgumentException($"{nameof(StringToColorModel)} {nameof(ColorModel)} '{ColorModelName}' is not supported");
            }
        }

        public static string ColorModelToString(ColorModel model)
        {
            switch (model)
            {
                case ColorModel.Bgr15:
                    return "Bgr15";
                case ColorModel.Abgr16:
                    return "Abgr16";
                case ColorModel.Rgba32:
                    return "Rgba32";
                case ColorModel.Nes:
                    return "Nes";
                default:
                    throw new ArgumentException($"{nameof(ColorModelToString)} {nameof(ColorModel)} '{model}' is not supported");
            }
        }

        public static IEnumerable<string> GetColorModelNames()
        {
            return Enum.GetNames(typeof(ColorModel)).Cast<string>().ToList();
        }

        public bool UnlinkResource(IProjectResource resource) => false;

        public IEnumerable<IProjectResource> LinkedResources
        {
            get
            {
                yield return DataFile;
            }
        }
    }
}
