using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Drawing;
//using ColorMine.ColorSpaces.Comparisons;
//using ColorMine.ColorSpaces.Conversions;
using ImageMagitek.Project;
using ImageMagitek.ExtensionMethods;

namespace ImageMagitek.Colors
{
    public enum ColorModel { RGBA32 = 0, RGB24, ARGB32, BGR15, ABGR16, RGB15, NES }

    /// <summary>
    /// Storage source of the palette
    /// ProjectFile palettes are stored in the XML project file
    /// </summary>
    public enum PaletteStorageSource { File = 0, ProjectFile, Json }

    /// <summary>
    /// Palette manages the loading of palettes and colors from a variety of color formats
    /// Local colors are internally ARGB32
    /// Foreign colors are the same as the target system
    /// </summary>
    public class Palette : IProjectResource
    {
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
        /// Address of the palette within the file
        /// </summary>
        public FileBitAddress FileAddress { get; private set; }

        /// <summary>
        /// Number of color entries in the palette
        /// </summary>
        public int Entries { get; private set; }

        /// <summary>
        /// Specifies if the Palette has an alpha channel
        /// </summary>
        public bool HasAlpha { get; private set; }

        /// <summary>
        /// Specifies if the palette's 0-index is automatically treated as transparent
        /// </summary>
        public bool ZeroIndexTransparent { get; private set; }

        /// <summary>
        /// Specifies the palette's storage source
        /// </summary>
        public PaletteStorageSource StorageSource { get; private set; }

        /// <summary>
        /// Gets the internal palette containing native Rgba32 colors
        /// </summary>
        ColorRgba32[] NativePalette { get => _nativePalette.Value; }
        Lazy<ColorRgba32[]> _nativePalette;

        /// <summary>
        /// Gets the internal palette containing foreign colors
        /// </summary>
        IColor32[] ForeignPalette { get => _foreignPalette.Value; }
        Lazy<IColor32[]> _foreignPalette;

        /// <summary>
        /// Constructs a new named Palette object
        /// </summary>
        /// <param name="PaletteName">Name of the palette</param>
        public Palette(string PaletteName)
        {
            Name = PaletteName;

            HasAlpha = false;
            ZeroIndexTransparent = true;
        }

        public Palette(string name, ColorModel colorModel, FileBitAddress fileAddress,
            int entries, bool zeroIndexTransparent, PaletteStorageSource storageSource)
        {
            Name = name;
            ColorModel = colorModel;
            FileAddress = fileAddress;
            Entries = entries;
            ZeroIndexTransparent = zeroIndexTransparent;
            StorageSource = storageSource;

            if(storageSource == PaletteStorageSource.File)
            {
                _nativePalette = new Lazy<ColorRgba32[]>(() => LoadNativePalette());
                _foreignPalette = new Lazy<IColor32[]>(() => LoadForeignPalette());
            }
            else if(storageSource == PaletteStorageSource.Json)
            {
                _nativePalette = new Lazy<ColorRgba32[]>(() => new ColorRgba32[Entries]);
                _foreignPalette = new Lazy<IColor32[]>(() => new IColor32[Entries]);
            }
        }

        /// <summary>
        /// Renames a Palette to a new name
        /// </summary>
        /// <param name="name"></param>
        public void Rename(string name)
        {
            Name = name;
        }

        /// <summary>
        /// Reloads the palette data from its underlying source
        /// </summary>
        /// <returns></returns>
        public bool Reload()
        {
            return LazyLoadPalette(DataFile, FileAddress, ColorModel, ZeroIndexTransparent, Entries);
        }

        /// <summary>
        /// Lazily loads palette from a previously opened file. Actual loading will occur during color access.
        /// </summary>
        /// <param name="dataFile">DataFile containing the palette data</param>
        /// <param name="address">File address to the beginning of the palette</param>
        /// <param name="model">ColorModel of the palette</param>
        /// <param name="zeroIndexTransparent">If the 0-index of the palette is automatically transparent</param>
        /// <param name="numEntries">Number of entries the palette contains</param>
        /// <returns>Success value</returns>
        public bool LazyLoadPalette(DataFile dataFile, FileBitAddress address, ColorModel model, bool zeroIndexTransparent, int numEntries)
        {
            if (numEntries > 256 || numEntries < 2)
                throw new ArgumentOutOfRangeException($"{nameof(LazyLoadPalette)}: {nameof(numEntries)} ({numEntries}) is out of range");

            DataFile = dataFile;
            FileAddress = address;
            ColorModel = model;
            ZeroIndexTransparent = zeroIndexTransparent;
            Entries = numEntries;
            StorageSource = PaletteStorageSource.File;

            _nativePalette = new Lazy<ColorRgba32[]>(() => LoadNativePalette());
            _foreignPalette = new Lazy<IColor32[]>(() => LoadForeignPalette());

            return true;
        }

        private ColorRgba32[] LoadNativePalette()
        {
            var nativePalette = new ColorRgba32[Entries];
            for(int i = 0; i < Entries; i++)
                nativePalette[i] = ColorConverter.ToNative(ForeignPalette[i]); // Will load ForeignPalette if not already loaded

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
        private IColor32[] LoadForeignPalette()
        {
            return PaletteBinarySerializer.ReadPalette(DataFile, FileAddress, ColorModel, Entries);
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

        public IColor32 GetForeignColor(int index)
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

        /// <summary>
        /// Returns a palette index matching the specified Native ARGB32 color
        /// </summary>
        /// <param name="color">NativeColor to search for</param>
        /// <param name="exactColorOnly">true to return only exactly matched colors; false to match the closest color</param>
        /// <returns>A palette index matching the specified color</returns>
        public byte GetIndexByNativeColor(ColorRgba32 color, bool exactColorOnly)
        {
            if (exactColorOnly)
            {
                for (byte i = 0; i < Entries; i++)
                {
                    if (NativePalette[i].Color == color.Color)
                        return i;
                }

                // Failed to find the exact color in the palette
                throw new Exception($"{nameof(GetIndexByNativeColor)} could not match exact color");
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
        public void SetForeignColor(int index, IColor32 foreignColor)
        {
            if (ForeignPalette is null)
                throw new NullReferenceException($"{nameof(SetForeignColor)} property '{nameof(ForeignPalette)}' was null");

            if (index >= Entries)
                throw new ArgumentOutOfRangeException($"{nameof(GetForeignColor)} parameter '{nameof(index)}' was out of range");

            ForeignPalette[index] = foreignColor;
            NativePalette[index] = ColorConverter.ToNative(foreignColor);
        }

        /// <summary>
        /// Replaces the color at the specified palette index with the specified foreign color
        /// Additionally, updates the native color in the palette
        /// </summary>
        /// <param name="index">Zero-based palette index</param>
        public void SetForeignColor(int index, byte R, byte G, byte B, byte A)
        {
            var fc = ColorFactory.CreateColor(ColorModel);
            fc.R = R;
            fc.G = G;
            fc.B = B;
            fc.A = A;

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
            ForeignPalette[index] = ColorConverter.ToForeign(nativeColor, ColorModel);
        }

        /// <summary>
        /// Replaces the color at the specified palette index with the specified native color
        /// Additionally, updates the foreign color in the palette
        /// </summary>
        /// <param name="index">Zero-based palette index</param>
        public void SetNativeColor(int index, byte R, byte G, byte B, byte A)
        {
            var nc = ColorFactory.CreateColor(ColorModel.RGBA32);
            nc.R = R;
            nc.G = G;
            nc.B = B;
            nc.A = A;

            SetForeignColor(index, nc);
        }

        /// <summary>
        /// Saves palette's foreign colors to its underlying source and location
        /// </summary>
        /// <returns>Success value</returns>
        public bool SavePalette()
        {
            if(StorageSource == PaletteStorageSource.File)
                PaletteBinarySerializer.WritePalette(DataFile, FileAddress, ForeignPalette);

            return true;
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
                case "RGB24":
                    return ColorModel.RGB24;
                case "ARGB32":
                    return ColorModel.ARGB32;
                case "BGR15":
                    return ColorModel.BGR15;
                case "ABGR16":
                    return ColorModel.ABGR16;
                case "RGB15":
                    return ColorModel.RGB15;
                case "NES":
                    return ColorModel.NES;
                default:
                    throw new ArgumentException($"{nameof(StringToColorModel)} {nameof(ColorModel)} '{ColorModelName}' is not supported");
            }
        }

        public static string ColorModelToString(ColorModel model)
        {
            switch (model)
            {
                case ColorModel.RGB24:
                    return "RGB24";
                case ColorModel.ARGB32:
                    return "ARGB32";
                case ColorModel.BGR15:
                    return "BGR15";
                case ColorModel.ABGR16:
                    return "ABGR16";
                case ColorModel.RGB15:
                    return "RGB15";
                case ColorModel.NES:
                    return "NES";
                default:
                    throw new ArgumentException($"{nameof(ColorModelToString)} {nameof(ColorModel)} '{model.ToString()}' is not supported");
            }
        }

        public static IEnumerable<string> GetColorModelNames()
        {
            return Enum.GetNames(typeof(ColorModel)).Cast<string>().ToList();
        }

        public IEnumerable<IProjectResource> LinkedResources()
        {
            yield return DataFile;
        }
    }
}
