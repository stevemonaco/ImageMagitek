using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;
using ImageMagitek.Colors;
using ImageMagitek.Project.Serialization;
using ImageMagitek.Services;
using ImageMagitek.UnitTests.Fixtures;

namespace ImageMagitek.UnitTests;

/// <summary>
/// Copies the sample and hand-written project fixtures to a directory and builds serializers that open them
/// with DefaultRgba32 as the global palette.
/// </summary>
public static class ProjectFixtures
{
    public const string AllFeatures = "AllFeatures";

    private static readonly Lazy<Palette> _defaultPalette = new(() =>
        new PaletteService(new ColorFactory()).ReadJsonPalette(Path.Combine(AppContext.BaseDirectory, "_palettes", "DefaultRgba32.json"))!);

    public static Palette DefaultPalette => _defaultPalette.Value;

    public static string ResourceSchemaPath => Path.Combine(AppContext.BaseDirectory, "_schemas", "ResourceSchema.xsd");

    public static XmlProjectSerializerFactory CreateSerializerFactory(IColorFactory colorFactory) =>
        new(ResourceSchemaPath, CodecFixture.Shared.CodecFactory, colorFactory, [DefaultPalette]);

    /// <summary>
    /// Copies <paramref name="fixture"/> (a sample zip name or <see cref="AllFeatures"/>) into <paramref name="directory"/>
    /// and returns the project file's path.
    /// </summary>
    public static string Create(string fixture, string directory)
    {
        if (fixture == AllFeatures)
        {
            var source = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Projects", AllFeatures);
            foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
            {
                var target = Path.Combine(directory, Path.GetRelativePath(source, file));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(file, target);
            }

            var data = Enumerable.Range(0, 0x400).Select(x => (byte)x).ToArray();
            File.WriteAllBytes(Path.Combine(directory, "Resources", "main.bin"), data);
            File.WriteAllBytes(Path.Combine(directory, "Resources", "sub.bin"), data);
            return Path.Combine(directory, "AllFeatures.xml");
        }

        ZipFile.ExtractToDirectory(Path.Combine(AppContext.BaseDirectory, "_xmlprojectsamples", fixture), directory);
        return Directory.EnumerateFiles(directory, "*.xml", SearchOption.AllDirectories)
            .Single(x => XDocument.Parse(File.ReadAllText(x)).Root!.Name.LocalName == "project");
    }
}
