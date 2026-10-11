using System;
using System.Collections.Generic;
using System.Linq;
using DemoArtGenerator.Scenes;
using ImageMagitek;
using ImageMagitek.Codec;
using ImageMagitek.Colors;

namespace DemoArtGenerator;

/// <summary>
/// One welcome screen banner: a 16-color palette, index 0 transparent, and the code that draws it.
/// </summary>
public sealed record Scene(string Name, IReadOnlyList<(byte R, byte G, byte B)> Colors, Action<Canvas> Draw);

/// <summary>
/// Authors the welcome screen's demo files: each a 256x96 SNES 4bpp banner followed by its 16-color BGR15 palette.
/// </summary>
public static class DemoArt
{
    private const string CodecName = "SNES 4bpp";
    private const int ElementsWide = Canvas.Width / 8;
    private const int ElementsHigh = Canvas.Height / 8;
    private const int TileBytes = Canvas.Width * Canvas.Height / 2;
    private const int PaletteEntries = 16;
    public const int FileLength = TileBytes + PaletteEntries * 2;

    /// <summary>
    /// The scenes in rotation order; the shipped files are named after their position so they sort in this order.
    /// </summary>
    public static IReadOnlyList<Scene> Scenes { get; } =
        [Shopfront.Scene, Sunset.Scene, Seaside.Scene, Forest.Scene, Peaks.Scene, Dungeon.Scene, Space.Scene];

    public static string FileName(Scene scene) => $"{Scenes.ToList().IndexOf(scene) + 1:00}-{scene.Name}.bin";

    public static byte[] Compose(Scene scene)
    {
        var canvas = new Canvas();
        scene.Draw(canvas);
        return canvas.Pixels;
    }

    /// <summary>
    /// Builds a demo file's bytes by encoding the scene through the SNES 4bpp codec.
    /// </summary>
    public static byte[] Generate(Scene scene, ICodecFactory codecFactory)
    {
        var codec = codecFactory.CreateCodec(CodecName)!;

        var source = new MemoryDataSource(scene.Name, FileLength);
        var palette = CreateFilePalette(scene.Name, source);
        for (int i = 0; i < PaletteEntries; i++)
            palette.SetForeignColor(i, scene.Colors[i].R, scene.Colors[i].G, scene.Colors[i].B, 0);
        palette.SavePalette();

        var arranger = new SequentialArranger(ElementsWide, ElementsHigh, source, palette, codecFactory, codec);
        var image = new IndexedImage(arranger);
        Compose(scene).CopyTo(image.Image, 0);
        image.SaveImage();

        return source.Read(BitAddress.Zero, FileLength * 8);
    }

    private static Palette CreateFilePalette(string name, DataSource source)
    {
        var colorSources = Enumerable.Range(0, PaletteEntries)
            .Select(i => (IColorSource)new FileColorSource(TileBytes + i * 2, Endian.Little))
            .ToList();

        return new Palette(name, new ColorFactory(), ColorModel.Bgr15, colorSources, true, PaletteStorageSource.ProjectXml, source);
    }
}
