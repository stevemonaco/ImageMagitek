using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using ImageMagitek;
using ImageMagitek.Codec;
using ImageMagitek.Colors;
using ImageMagitek.Services;
using Microsoft.Extensions.Logging;
using TileShop.Shared.Services;
using TileShop.UI.Imaging;

namespace TileShop.UI.ViewModels;

/// <summary>
/// The empty document area's content: the demo banner, the File menu's ways in, recent projects and the version.
/// </summary>
public partial class WelcomeViewModel : ObservableObject
{
    public const int BannerWidth = 256;
    public const int BannerHeight = 96;
    public const int DemoFileLength = 12320;

    private const string CodecName = "SNES 4bpp";
    private const int ElementsWide = 32;
    private const int ElementsHigh = 12;
    private const int PaletteEntries = 16;
    private const int MaxScale = 4;

    public MenuViewModel Menu { get; }
    public EditorsViewModel Editors { get; }

    [ObservableProperty] private IndexedBitmapAdapter? _banner;
    [ObservableProperty] private bool _showTitleText;

    public WelcomeViewModel(MenuViewModel menu, EditorsViewModel editors, ICodecService codecService, IColorFactory colorFactory,
        UserPreferencesStore preferencesStore, ILogger<WelcomeViewModel> logger)
    {
        Menu = menu;
        Editors = editors;

        var demoDirectory = Path.Combine(AppContext.BaseDirectory, "_demo");
        var (demoFile, nextRotation) = PickDemoFile(demoDirectory, preferencesStore.Preferences.WelcomeRotation);
        preferencesStore.Preferences.WelcomeRotation = nextRotation;

        if (demoFile is null)
        {
            logger.LogWarning("No demo files were found in '{Directory}'", demoDirectory);
            ShowTitleText = true;
            return;
        }

        Task.Run(() => DecodeBanner(demoFile, codecService.CodecFactory, colorFactory, logger))
            .ContinueWith(task => Dispatcher.UIThread.Post(() =>
            {
                if (task.Result is { } image)
                    Banner = new IndexedBitmapAdapter(image);
                else
                    ShowTitleText = true;
            }));
    }

    /// <summary>
    /// Picks this launch's demo file from the <c>*.bin</c> files in <paramref name="directory"/>, sorted by name.
    /// </summary>
    /// <returns>The file at <paramref name="rotation"/>, wrapped to the file count, and the rotation for the next launch; a null path when there are no files</returns>
    public static (string? Path, int NextRotation) PickDemoFile(string directory, int rotation)
    {
        var files = Directory.Exists(directory)
            ? Directory.GetFiles(directory, "*.bin").Order(StringComparer.OrdinalIgnoreCase).ToArray()
            : [];

        if (files.Length == 0)
            return (null, rotation);

        var index = (rotation % files.Length + files.Length) % files.Length;
        return (files[index], (index + 1) % files.Length);
    }

    /// <summary>
    /// Decodes the demo file as a sequential SNES 4bpp arranger with the 16 Bgr15 colors at the file's end.
    /// </summary>
    /// <returns>The banner, or null after logging a warning when the file is missing, has the wrong length or fails to decode</returns>
    public static IndexedImage? DecodeBanner(string path, ICodecFactory codecFactory, IColorFactory colorFactory, ILogger logger)
    {
        try
        {
            var bytes = File.ReadAllBytes(path);
            if (bytes.Length != DemoFileLength)
                throw new InvalidDataException($"It is {bytes.Length} bytes, not {DemoFileLength}");

            var codec = codecFactory.CreateCodec(CodecName)!;

            var source = new MemoryDataSource(Path.GetFileNameWithoutExtension(path), bytes.Length);
            source.Write(bytes);

            var colorSources = Enumerable.Range(0, PaletteEntries)
                .Select(i => (IColorSource)new FileColorSource(bytes.Length - PaletteEntries * 2 + i * 2, Endian.Little))
                .ToList();
            var palette = new Palette(Path.GetFileNameWithoutExtension(path), colorFactory, ColorModel.Bgr15, colorSources, true, PaletteStorageSource.ProjectXml, source);
            _ = palette[0]; // Colors load lazily; load them here so a failure is caught off the UI thread

            var arranger = new SequentialArranger(ElementsWide, ElementsHigh, source, palette, codecFactory, codec);
            return new IndexedImage(arranger);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "The demo file '{Path}' was not shown: {Reason}", path, ex.Message);
            return null;
        }
    }

    /// <summary>
    /// The largest whole scale from 1 to 4 at which the banner fits <paramref name="width"/>; 1 when even 1x does not fit.
    /// </summary>
    public static int ScaleFor(double width) => Math.Clamp((int)(width / BannerWidth), 1, MaxScale);
}
