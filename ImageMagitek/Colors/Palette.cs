using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Linq;
using CommunityToolkit.Diagnostics;
using ImageMagitek.Colors.Serialization;
using ImageMagitek.ExtensionMethods;
using ImageMagitek.Project;

namespace ImageMagitek.Colors;

//public enum ColorModel { RGBA32 = 0, RGB24, ARGB32, BGR15, ABGR16, RGB15, NES, BGR9, BGR6 }
public enum ColorModel { Rgba32 = 0, Bgr15 = 3, Abgr16 = 4, Nes = 6, Bgr9 = 7, Bgr6 = 8, Rgb15 = 9 }

/// <summary>
/// Storage source of the palette
/// ProjectXml has IColorSources within DataFile and/or the palette XML project file
/// GlobalJson is for predefined, global palettes
/// </summary>
public enum PaletteStorageSource { ProjectXml, GlobalJson }

/// <summary>
/// Palette manages the loading of palettes and colors from a variety of color formats
/// Local colors are internally ColorRgba32
/// Foreign colors are the same as the target system
/// </summary>
public class Palette : IProjectResource
{
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
    /// DataSource which contains the palette colors specified by FileColorSources. Null if the underlying source cannot be saved such as GlobalJson.
    /// </summary>
    public DataSource? DataSource { get; }

    /// <summary>
    /// Number of color entries in the palette
    /// </summary>
    public int Entries => ColorSources?.Length ?? 0;

    /// <summary>
    /// Specifies if the Palette has an alpha channel
    /// </summary>
    public bool HasAlpha { get; }

    /// <summary>
    /// Specifies if the palette's 0-index is automatically treated as transparent
    /// </summary>
    public bool ZeroIndexTransparent { get; set; }

    /// <summary>
    /// Specifies the palette's storage source
    /// </summary>
    public PaletteStorageSource StorageSource { get; }

    /// <summary>
    /// Specifies how the Palette colors will be serialized
    /// </summary>
    public IColorSource[] ColorSources { get; private set; }

    /// <summary>
    /// Gets the internal palette containing native Rgba32 colors
    /// </summary>
    private ColorRgba32[] NativePalette { get => _nativePalette.Value; }
    private Lazy<ColorRgba32[]> _nativePalette;

    /// <summary>
    /// Gets the internal palette containing foreign colors
    /// </summary>
    private IColor[] ForeignPalette { get => _foreignPalette.Value; }
    private Lazy<IColor[]> _foreignPalette;

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

        Reload();
    }

    public Palette(string name, IColorFactory colorFactory, ColorModel colorModel, IList<IColorSource> colorSources,
        bool zeroIndexTransparent, PaletteStorageSource storageSource, DataSource dataSource)
    {
        Name = name;
        _colorFactory = colorFactory;
        _colorSerializer = new ColorSourceSerializer(_colorFactory);

        ColorModel = colorModel;
        ColorSources = colorSources.ToArray();
        ZeroIndexTransparent = zeroIndexTransparent;
        StorageSource = storageSource;
        DataSource = dataSource;

        Reload();
    }

    /// <summary>
    /// Lazily reloads the palette data from its underlying source
    /// </summary>
    [MemberNotNull(nameof(_nativePalette), nameof(_foreignPalette))]
    public void Reload()
    {
        if (StorageSource is PaletteStorageSource.ProjectXml or PaletteStorageSource.GlobalJson)
        {
            _nativePalette = new Lazy<ColorRgba32[]>(LoadNativePalette);
            _foreignPalette = new Lazy<IColor[]>(LoadForeignPalette);
        }
        else
            throw new NotSupportedException($"{nameof(PaletteStorageSource)} of type '{StorageSource}' is not supported");
    }

    private ColorRgba32[] LoadNativePalette()
    {
        var nativePalette = new ColorRgba32[Entries];

        if (StorageSource == PaletteStorageSource.ProjectXml)
        {
            for (int i = 0; i < Entries; i++)
                nativePalette[i] = _colorFactory.ToNative(ForeignPalette[i]); // Will load ForeignPalette if not already loaded

            return nativePalette;
        }
        else if (StorageSource == PaletteStorageSource.GlobalJson)
        {
            for (int i = 0; i < Entries; i++)
            {
                if (ColorSources[i] is ProjectNativeColorSource nativeColor)
                    nativePalette[i] = nativeColor.Value;
                else if (ColorSources[i] is ProjectForeignColorSource foreignColor)
                    nativePalette[i] = _colorFactory.ToNative(foreignColor.Value);
            }

            return nativePalette;
        }

        throw new NotSupportedException();
    }

    /// <summary>
    /// Loads the ForeignPalette from current settings
    /// </summary>
    private IColor[] LoadForeignPalette()
    {
        if (StorageSource == PaletteStorageSource.ProjectXml)
        {
            Guard.IsNotNull(DataSource);
            return _colorSerializer.LoadColors(ColorSources, DataSource, ColorModel, Entries);
        }
        
        if (StorageSource == PaletteStorageSource.GlobalJson)
        {
            var foreignPalette = new IColor[Entries];

            for (int i = 0; i < Entries; i++)
            {
                if (ColorSources[i] is ProjectForeignColorSource foreignColor)
                    foreignPalette[i] = foreignColor.Value;
                else if (ColorSources[i] is ProjectNativeColorSource nativeColor)
                    foreignPalette[i] = _colorFactory.ToForeign(nativeColor.Value, ColorModel);
            }

            return foreignPalette;
        }

        throw new NotSupportedException($"{nameof(LoadForeignPalette)}: {nameof(PaletteStorageSource)} of type '{StorageSource}' is not supported");
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


    /// <summary>
    /// Finds the palette index matching the specified native color
    /// </summary>
    /// <returns>False when no entry is acceptable for the strategy</returns>
    public bool TryGetIndexByNativeColor(ColorRgba32 color, ColorMatchStrategy matchStrategy, out byte index)
    {
        var matcher = new PaletteColorMatcher(this, matchStrategy);

        if (matcher.TryMatch(color, out var match))
        {
            index = match.Index;
            return true;
        }

        index = default;
        return false;
    }

    /// <summary>
    /// Returns the palette index matching the specified native color
    /// </summary>
    /// <exception cref="ArgumentException">No entry is acceptable for the strategy</exception>
    public byte GetIndexByNativeColor(ColorRgba32 color, ColorMatchStrategy matchStrategy)
    {
        if (TryGetIndexByNativeColor(color, matchStrategy, out var index))
            return index;

        throw new ArgumentException($"{nameof(GetIndexByNativeColor)} could not match color (R: {color.R}, G: {color.G}, B: {color.B}, A: {color.A}) within palette '{Name}'");
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
    public void SetForeignColor(int index, byte r, byte g, byte b, byte a)
    {
        var fc = _colorFactory.CreateColor(ColorModel, r, g, b, a);

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
    public void SetNativeColor(int index, byte r, byte g, byte b, byte a)
    {
        var nc = _colorFactory.CreateColor(ColorModel.Rgba32, r, g, b, a);

        SetForeignColor(index, nc);
    }

    /// <summary>
    /// Saves palette's foreign colors to its underlying source and location
    /// </summary>
    /// <returns>True if the palette can be saved, false if the palette is not valid to be saved</returns>
    public bool SavePalette()
    {
        if (StorageSource == PaletteStorageSource.ProjectXml && DataSource is not null)
        {
            _colorSerializer.StoreColors(ColorSources, DataSource, NativePalette, ForeignPalette);
            return true;
        }

        return false;
    }

    public void SetColorSources(IEnumerable<IColorSource> colorSources)
    {
        ColorSources = colorSources.ToArray();
        Reload();
    }

    /// <summary>
    /// Gets the string name associated with a ColorModel object
    /// </summary>
    /// <param name="colorModelName">Name of the ColorModel to retrieve</param>
    /// <returns>A string name describing the ColorModel</returns>
    public static ColorModel StringToColorModel(string colorModelName)
    {
        return colorModelName switch
        {
            "Rgba32" => ColorModel.Rgba32,
            "Rgb15" => ColorModel.Rgb15,
            "Bgr15" => ColorModel.Bgr15,
            "Abgr16" => ColorModel.Abgr16,
            "Nes" => ColorModel.Nes,
            "Bgr9" => ColorModel.Bgr9,
            "Bgr6" => ColorModel.Bgr6,
            _ => throw new ArgumentException($"{nameof(StringToColorModel)} {nameof(ColorModel)} '{colorModelName}' is not supported"),
        };
    }

    public static string ColorModelToString(ColorModel model)
    {
        return model switch
        {
            ColorModel.Rgba32 => "Rgba32",
            ColorModel.Rgb15 => "Rgb15",
            ColorModel.Bgr15 => "Bgr15",
            ColorModel.Abgr16 => "Abgr16",
            ColorModel.Nes => "Nes",
            ColorModel.Bgr9 => "Bgr9",
            ColorModel.Bgr6 => "Bgr6",
            _ => throw new ArgumentException($"{nameof(ColorModelToString)} {nameof(ColorModel)} '{model}' is not supported"),
        };
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
            if (DataSource is not null)
                yield return DataSource;
        }
    }
}
