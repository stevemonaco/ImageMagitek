using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using ImageMagitek.Colors;
using ImageMagitek.Metatiles;
using ImageMagitek.Project.Serialization;
using ImageMagitek.Tilemaps;

namespace ImageMagitek.UnitTests;

/// <summary>
/// Fully populated, non-default serialization models, and per-type value changes, for tests that cover every model property
/// </summary>
public static class ModelSamples
{
    public static IReadOnlyList<Type> Types { get; } = typeof(ResourceModel).Assembly.GetTypes()
        .Where(x => !x.IsAbstract && (x.IsSubclassOf(typeof(ResourceModel)) || x.IsAssignableTo(typeof(IColorSourceModel))))
        .Append(typeof(ArrangerElementModel))
        .ToList();

    public static IReadOnlyList<Type> RegenerableTypes { get; } =
    [
        typeof(CompressedDataModel), typeof(PaletteModel), typeof(MetatileSetModel), typeof(MetatileMapModel), typeof(TilemapModel),
        typeof(ScatteredArrangerModel)
    ];

    /// <summary>
    /// Builds a new sample of <paramref name="type"/> with fresh collections; <paramref name="reversed"/> inserts slot dictionaries in reverse order
    /// </summary>
    public static object Create(Type type, bool reversed = false) => type.Name switch
    {
        nameof(ImageProjectModel) => new ImageProjectModel { Name = "Project", Root = "Root", Version = new Version(1, 1) },
        nameof(ResourceFolderModel) => new ResourceFolderModel { Name = "Folder" },
        nameof(DataFileModel) => new DataFileModel { Name = "Rom", Location = "rom.bin" },
        nameof(CompressedDataModel) => new CompressedDataModel { Name = "Compressed", ParentKey = "/Rom", FileOffset = 0x40, Compression = "Lz77" },
        nameof(PaletteModel) => new PaletteModel
        {
            Name = "Palette",
            ColorModel = ColorModel.Bgr15,
            DataFileKey = "/Rom",
            ZeroIndexTransparent = true,
            StorageSource = PaletteStorageSource.ProjectXml,
            ColorSources = Types.Where(x => x.IsAssignableTo(typeof(IColorSourceModel))).Select(x => (IColorSourceModel)Create(x)).ToList()
        },
        nameof(ScatteredArrangerModel) => new ScatteredArrangerModel
        {
            Name = "Arranger",
            ElementGrid = Grid(),
            ArrangerElementSize = new Size(2, 2),
            ElementPixelSize = new Size(8, 8),
            Layout = ElementLayout.Tiled,
            ColorType = PixelColorType.Indexed
        },
        nameof(TilemapModel) => new TilemapModel
        {
            Name = "Tilemap",
            MapDataKey = "/Rom",
            MapOffset = 0x100,
            Width = 32,
            Height = 28,
            Layout = MapScreenLayout.Snes32,
            FormatName = "Snes",
            TileDataKey = "/Tiles",
            TileOffset = 0x200,
            CodecName = "SNES 4bpp",
            FirstTile = 1,
            EmptyTile = 2,
            TileCount = 3,
            TilePaletteKey = "/Arranger",
            PaletteSlots = Slots(reversed)
        },
        nameof(MetatileSetModel) => new MetatileSetModel
        {
            Name = "Set",
            MetatileDataKey = "/Rom",
            MetatileOffset = 0x300,
            MetatileCount = 64,
            MetatileWidth = 2,
            MetatileHeight = 2,
            Layers = 2,
            FormatName = "Gba",
            Order = MetatileOrder.Planar,
            TileDataKey = "/Tiles",
            TileOffset = 0x400,
            CodecName = "GBA 4bpp",
            FirstTile = 1,
            TileCount = 5,
            PaletteSlots = Slots(reversed),
            AttributeDataKey = "/Attributes",
            AttributeOffset = 0x500,
            AttributeSize = 4
        },
        nameof(MetatileMapModel) => new MetatileMapModel
        {
            Name = "Map",
            MapDataKey = "/Rom",
            MapOffset = 0x600,
            Width = 20,
            Height = 15,
            Layout = MapScreenLayout.Snes32,
            FormatName = "Gba",
            EmptyBlock = 6,
            Sets = [("/Set A", 0), ("/Set B", 512)]
        },
        nameof(ArrangerElementModel) => Element(0, 0),
        nameof(FileColorSourceModel) => new FileColorSourceModel(0x20, 16, Endian.Big),
        nameof(ProjectNativeColorSourceModel) => new ProjectNativeColorSourceModel(new ColorRgba32(0x11223344)),
        nameof(ProjectForeignColorSourceModel) => new ProjectForeignColorSourceModel(new ColorBgr15(0x7C1F)),
        nameof(ScatteredColorSourceModel) => new ScatteredColorSourceModel(2, [new ColorFieldRun(new ColorField(0x10, 4, 4, 0), 8)]),
        _ => throw new NotSupportedException($"No sample for '{type.Name}'")
    };

    /// <summary>
    /// Returns a value of <paramref name="type"/> that differs from <paramref name="value"/>
    /// </summary>
    public static object? Vary(object? value, Type type)
    {
        if (type == typeof(string))
            return value is string text ? text + "x" : "x";
        if (type == typeof(int))
            return (int)value! + 1;
        if (type == typeof(long))
            return (long)value! + 1;
        if (type == typeof(int?))
            return value is int number ? number + 1 : 1;
        if (type == typeof(bool))
            return !(bool)value!;
        if (type.IsEnum)
        {
            var values = Enum.GetValues(type);
            var index = Array.IndexOf(values, value);
            return values.GetValue((index + 1) % values.Length);
        }
        if (type == typeof(Size))
            return (Size)value! + new Size(1, 0);
        if (type == typeof(Version))
            return new Version(((Version)value!).Major, ((Version)value!).Minor + 1);
        if (type == typeof(BitAddress))
            return new BitAddress(((BitAddress)value!).ByteOffset + 1, ((BitAddress)value!).BitOffset);
        if (type == typeof(IReadOnlyList<IColorSourceModel>))
            return ((IReadOnlyList<IColorSourceModel>)value!).Append(new ProjectNativeColorSourceModel(new ColorRgba32(1))).ToList();
        if (type == typeof(IReadOnlyDictionary<int, string>))
            return ((IReadOnlyDictionary<int, string>)value!).ToDictionary(x => x.Key, x => x.Value + "x");
        if (type == typeof(IReadOnlyList<(string SetKey, int Base)>))
            return ((IReadOnlyList<(string, int)>)value!).Append(("/Set C", 1024)).ToList();
        if (type == typeof(IReadOnlyList<ColorFieldRun>))
            return ((IReadOnlyList<ColorFieldRun>)value!).Append(new ColorFieldRun(new ColorField(0x20, 0, 4, 4), 8)).ToList();
        if (type == typeof(ArrangerElementModel[,]))
        {
            var grid = (ArrangerElementModel[,])((ArrangerElementModel[,])value!).Clone();
            grid[0, 0] = grid[0, 0] with { CodecName = grid[0, 0].CodecName + "x" };
            return grid;
        }
        if (type == typeof(ColorRgba32))
            return new ColorRgba32(((ColorRgba32)value!).Color + 1);
        if (type == typeof(IColor))
            return new ColorBgr15(((IColor)value!).Color ^ 1);

        throw new NotSupportedException($"No variation for property type '{type}'; add one to {nameof(ModelSamples)}.{nameof(Vary)}");
    }

    private static Dictionary<int, string> Slots(bool reversed)
    {
        var slots = new[] { (0, "/Palette A"), (2, "/Palette B"), (5, "/Palette C") };
        return (reversed ? slots.Reverse() : slots).ToDictionary(x => x.Item1, x => x.Item2);
    }

    private static ArrangerElementModel[,] Grid()
    {
        var grid = new ArrangerElementModel[2, 2];
        grid[0, 0] = Element(0, 0);
        grid[1, 0] = Element(1, 0) with { PaletteKey = null };
        grid[0, 1] = Element(0, 1);
        return grid;
    }

    private static ArrangerElementModel Element(int x, int y) => new()
    {
        DataFileKey = "/Rom",
        PaletteKey = "/Palette",
        CodecName = "SNES 4bpp",
        FileAddress = new BitAddress(0x80 + 32 * (x + 2 * y), 2),
        PositionX = x,
        PositionY = y,
        Mirror = MirrorOperation.Vertical,
        Rotation = RotationOperation.Left
    };
}
