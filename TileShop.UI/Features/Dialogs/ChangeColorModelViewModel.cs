using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using ImageMagitek;
using ImageMagitek.Colors;
using ImageMagitek.Colors.Serialization;
using TileShop.Shared.Interactions;
using TileShop.UI.Converters;
using TileShop.UI.Models;

namespace TileShop.UI.ViewModels;

public sealed record ColorModelChange(ColorModel Model, IReadOnlyList<IColorSource> Sources);

/// <summary>
/// Picks a new color model for a palette and previews its sources reinterpreted under it
/// </summary>
public partial class ChangeColorModelViewModel : RequestViewModel<ColorModelChange>
{
    private readonly Palette _palette;
    private readonly IColorFactory _colorFactory;
    private IReadOnlyList<IColorSource>? _sources;

    public string CurrentColorModel { get; }
    public IReadOnlyList<string> ColorModels { get; } = Palette.GetColorModelNames().ToList();
    public ObservableCollection<PaletteSwatchModel> BeforeColors { get; } = [];
    public ObservableCollection<PaletteSwatchModel> AfterColors { get; } = [];

    [ObservableProperty] private string _selectedColorModel;
    [ObservableProperty] private string _previewError = "";

    public ChangeColorModelViewModel(Palette palette, IColorFactory colorFactory)
    {
        _palette = palette;
        _colorFactory = colorFactory;
        Title = "Change Color Model";
        AcceptName = "Change";

        CurrentColorModel = palette.ColorModel.ToString();
        _selectedColorModel = CurrentColorModel;

        for (int i = 0; i < palette.Entries; i++)
            BeforeColors.Add(ToSwatch(i, palette.GetNativeColor(i)));

        UpdatePreview();
    }

    partial void OnSelectedColorModelChanged(string value)
    {
        UpdatePreview();
        TryAcceptCommand.NotifyCanExecuteChanged();
    }

    protected override bool CanAccept() => _sources is not null && SelectedColorModel != CurrentColorModel;

    public override ColorModelChange? ProduceResult() =>
        _sources is null ? null : new ColorModelChange(Palette.StringToColorModel(SelectedColorModel), _sources);

    /// <summary>
    /// Regenerates <paramref name="palette"/>'s sources under <paramref name="model"/>. Contiguous file ranges keep their start and
    /// count with offsets spaced by the new color size, and foreign values keep their raw bits.
    /// </summary>
    public static IReadOnlyList<IColorSource> ReinterpretSources(Palette palette, ColorModel model, IColorFactory colorFactory)
    {
        var oldSize = colorFactory.CreateColor(palette.ColorModel).Size;
        var newSize = colorFactory.CreateColor(model).Size;
        var result = new List<IColorSource>();
        var sources = palette.ColorSources;

        int i = 0;
        while (i < sources.Length)
        {
            switch (sources[i])
            {
                case FileColorSource first:
                    int count = Palette.GetFileRunLength(sources, i, oldSize);

                    for (int j = 0; j < count; j++)
                        result.Add(new FileColorSource(first.Offset + j * newSize, first.Endian));

                    i += count;
                    continue;
                case ProjectNativeColorSource native:
                    result.Add(new ProjectNativeColorSource(native.Value));
                    break;
                case ProjectForeignColorSource foreign:
                    result.Add(new ProjectForeignColorSource(colorFactory.CreateColor(model, foreign.Value.Color)));
                    break;
                default:
                    throw new NotSupportedException($"Color source of type '{sources[i].GetType()}' cannot change color model");
            }

            i++;
        }

        return result;
    }

    private void UpdatePreview()
    {
        AfterColors.Clear();
        PreviewError = "";
        _sources = null;

        try
        {
            var model = Palette.StringToColorModel(SelectedColorModel);

            if (_palette.DataSource is null)
                throw new InvalidOperationException("The palette has no data source");

            if (FindOutOfRangeEntry(model, _palette.DataSource) is { } error)
            {
                PreviewError = error;
                return;
            }

            var sources = ReinterpretSources(_palette, model, _colorFactory);
            var preview = new Palette(_palette.Name, _colorFactory, model, sources.ToList(), _palette.ZeroIndexTransparent,
                PaletteStorageSource.ProjectXml, _palette.DataSource);

            for (int i = 0; i < preview.Entries; i++)
                AfterColors.Add(ToSwatch(i, preview.GetNativeColor(i)));

            _sources = sources;
        }
        catch (Exception ex)
        {
            AfterColors.Clear();
            PreviewError = $"Cannot preview this color model: {ex.Message}";
        }
    }

    /// <summary>
    /// Finds the first entry whose stored value is not a valid color under a table-based <paramref name="model"/> such as NES
    /// </summary>
    /// <returns>A message describing the invalid entry, or null when every entry is valid</returns>
    private string? FindOutOfRangeEntry(ColorModel model, DataSource dataSource)
    {
        if (_colorFactory.CreateColor(model) is not ITableColor table)
            return null;

        var oldSize = _colorFactory.CreateColor(_palette.ColorModel).Size;
        var sources = _palette.ColorSources;

        int i = 0;
        while (i < sources.Length)
        {
            var count = Math.Max(Palette.GetFileRunLength(sources, i, oldSize), 1);

            for (int j = 0; j < count; j++)
            {
                uint? value = sources[i] switch
                {
                    FileColorSource first => ColorSourceSerializer.ReadFileColorValue(dataSource, first.Offset + j * table.Size, table.Size, first.Endian),
                    ProjectForeignColorSource foreign => foreign.Value.Color,
                    _ => null
                };

                if (value > (uint)table.ColorMax)
                    return $"Entry {i + j} holds 0x{value:X2}, which is not a valid {model} color (0x00-0x{table.ColorMax:X2})";
            }

            i += count;
        }

        return null;
    }

    private static PaletteSwatchModel ToSwatch(int index, ColorRgba32 native) =>
        new(index, ColorRgba32ToMediaColorConverter.ToMediaColor(native));
}
