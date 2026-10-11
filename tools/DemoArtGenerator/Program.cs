using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using DemoArtGenerator;
using ImageMagitek.Codec;
using ImageMagitek.Colors;
using ImageMagitek.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

var paletteService = new PaletteService(new ColorFactory());
var palette = paletteService.ReadJsonPalette(Path.Combine(AppContext.BaseDirectory, "_palettes", "Classic.json"))!;
var codecFactory = new CodecFactory(palette, []);
var codecService = new XmlCodecService(Path.Combine(AppContext.BaseDirectory, "_schemas", "CodecSchema.xsd"), codecFactory);

var failures = codecService.LoadCodecs(Path.Combine(AppContext.BaseDirectory, "_codecs"));
if (failures.Count > 0)
{
    Console.Error.WriteLine(string.Join(Environment.NewLine, failures.Select(x => $"{x.FileName}: {x.Message}")));
    return 1;
}

var uiDirectory = Path.GetFullPath(Path.Combine(ThisDir(), "..", "..", "TileShop.UI"));
var demoDirectory = Path.Combine(uiDirectory, "_demo");
foreach (var stale in Directory.GetFiles(demoDirectory, "*.bin"))
    File.Delete(stale);

foreach (var scene in DemoArt.Scenes)
{
    var path = Path.Combine(demoDirectory, DemoArt.FileName(scene));
    File.WriteAllBytes(path, DemoArt.Generate(scene, codecFactory));
    Console.WriteLine($"Wrote {path}");
}

Logo.WriteIcon(Path.Combine(uiDirectory, "Assets", "tsicon.ico"));
Logo.WriteSvg(Path.Combine(uiDirectory, "Assets", "tslogo.svg"));
using (var titleBarIcon = Logo.Render(2))
    titleBarIcon.SaveAsPng(Path.Combine(uiDirectory, "Assets", "tslogo.png"));
Console.WriteLine("Wrote Assets/tsicon.ico, tslogo.svg and tslogo.png");

if (args is ["--preview", var previewPath, ..])
{
    WritePreview(previewPath, args.Length > 2 ? DemoArt.Scenes.Where(s => s.Name == args[2]).ToList() : DemoArt.Scenes);
    Console.WriteLine($"Wrote {previewPath}");
}

return 0;

static void WritePreview(string path, System.Collections.Generic.IReadOnlyList<Scene> scenes)
{
    var scale = scenes.Count == 1 ? 4 : 2;
    const int gap = 8;
    using var sheet = new Image<Rgba32>(Canvas.Width * scale + gap * 2, scenes.Count * (Canvas.Height * scale + gap) + gap, Rgba32.ParseHex("2b2d3a"));

    for (int i = 0; i < scenes.Count; i++)
    {
        var scene = scenes[i];
        var pixels = DemoArt.Compose(scene);
        var top = gap + i * (Canvas.Height * scale + gap);
        for (int y = 0; y < Canvas.Height * scale; y++)
        {
            for (int x = 0; x < Canvas.Width * scale; x++)
            {
                var index = pixels[y / scale * Canvas.Width + x / scale];
                if (index == 0)
                    continue;
                var (r, g, b) = scene.Colors[index];
                sheet[gap + x, top + y] = new Rgba32((byte)(r * 255 / 31), (byte)(g * 255 / 31), (byte)(b * 255 / 31));
            }
        }
    }

    sheet.SaveAsPng(path);
}

static string ThisDir([CallerFilePath] string path = "") => Path.GetDirectoryName(path)!;
