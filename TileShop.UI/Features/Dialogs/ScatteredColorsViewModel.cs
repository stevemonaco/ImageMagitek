using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageMagitek.Colors;
using TileShop.Shared.Interactions;
using TileShop.Shared.Models;
using TileShop.UI.Converters;
using TileShop.UI.Models;

namespace TileShop.UI.ViewModels;

/// <summary>
/// One editable field of a scattered color run
/// </summary>
public partial class ColorFieldModel : ObservableValidator
{
    [ObservableProperty]
    [NotifyDataErrorInfo]
    [CustomValidation(typeof(ColorFieldModel), nameof(ValidateOffsetText))]
    private string _offsetText;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Range(0, 7, ErrorMessage = "Low bit must be 0-7")]
    private int _lowBit;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Range(1, 8, ErrorMessage = "Bits must be 1-8")]
    private int _bits;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Range(0, 31, ErrorMessage = "Shift must be 0-31")]
    private int _shift;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Range(1, int.MaxValue, ErrorMessage = "Stride must be at least 1 bit")]
    private int _stride;

    /// <summary>
    /// The field's own validation error, or one found by checking it against the other fields, the color model and the data file
    /// </summary>
    [ObservableProperty] private string _error = "";

    public ColorFieldModel(ColorFieldRun run)
    {
        _offsetText = run.First.Offset.ToString("X");
        _lowBit = run.First.LowBit;
        _bits = run.First.Bits;
        _shift = run.First.Shift;
        _stride = run.Stride;
        ValidateAllProperties();
    }

    public bool TryGetRun(out ColorFieldRun run)
    {
        if (HasErrors || !JumpToOffsetViewModel.TryParseOffset(OffsetText, NumericBase.Hexadecimal, out var offset))
        {
            run = default;
            return false;
        }

        run = new ColorFieldRun(new ColorField(offset, LowBit, Bits, Shift), Stride);
        return true;
    }

    public static ValidationResult ValidateOffsetText(string? text, ValidationContext context) =>
        JumpToOffsetViewModel.TryParseOffset(text, NumericBase.Hexadecimal, out _)
            ? ValidationResult.Success!
            : new ValidationResult("Not a valid hexadecimal offset");
}

/// <summary>
/// Edits a run of scattered colors: an entry count plus fields, with a preview of the colors they decode to
/// </summary>
public partial class ScatteredColorsViewModel : RequestViewModel<ScatteredColorSourceModel>
{
    private readonly Palette _palette;
    private readonly IColorFactory _colorFactory;
    private List<ColorFieldRun>? _runs;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Range(1, 256, ErrorMessage = "Entries must be 1-256")]
    private int _entries;

    [ObservableProperty] private string _previewError = "";

    public ObservableCollection<ColorFieldModel> Fields { get; } = [];
    public ObservableCollection<PaletteSwatchModel> PreviewColors { get; } = [];

    /// <param name="existing">The run to edit, or null to start a new one</param>
    public ScatteredColorsViewModel(Palette palette, IColorFactory colorFactory, ScatteredColorSourceModel? existing)
    {
        _palette = palette;
        _colorFactory = colorFactory;
        Title = "Scattered Colors";

        var runs = existing?.Fields ?? CreateDefaultRuns(colorFactory.CreateColor(palette.ColorModel).Size);
        _entries = existing?.Entries ?? 1;

        foreach (var run in runs)
            AddFieldModel(new ColorFieldModel(run));

        Fields.CollectionChanged += OnFieldsChanged;
        UpdatePreview();
    }

    /// <summary>
    /// The layout of a little-endian file color at offset 0: one field per byte, 8 bits each save the last, which takes the remainder
    /// </summary>
    public static List<ColorFieldRun> CreateDefaultRuns(int colorSize)
    {
        var count = (colorSize + 7) / 8;
        return Enumerable.Range(0, count)
            .Select(i => new ColorFieldRun(new ColorField(i, 0, i == count - 1 ? colorSize - 8 * i : 8, 8 * i), 8))
            .ToList();
    }

    [RelayCommand]
    private void AddField()
    {
        var last = Fields.LastOrDefault();
        var next = last is not null && last.TryGetRun(out var run)
            ? new ColorFieldRun(new ColorField(run.First.Offset + 1, 0, 8, Math.Min(run.First.Shift + run.First.Bits, 31)), run.Stride)
            : new ColorFieldRun(new ColorField(0, 0, 8, 0), 8);

        AddFieldModel(new ColorFieldModel(next));
    }

    [RelayCommand]
    private void RemoveField(ColorFieldModel field)
    {
        field.PropertyChanged -= OnFieldPropertyChanged;
        Fields.Remove(field);
    }

    partial void OnEntriesChanged(int value) => UpdatePreview();

    protected override bool CanAccept() => _runs is not null;

    public override ScatteredColorSourceModel? ProduceResult() =>
        _runs is null ? null : new ScatteredColorSourceModel(Entries, _runs);

    private void AddFieldModel(ColorFieldModel field)
    {
        field.PropertyChanged += OnFieldPropertyChanged;
        Fields.Add(field);
    }

    private void OnFieldsChanged(object? sender, NotifyCollectionChangedEventArgs e) => UpdatePreview();

    private void OnFieldPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ColorFieldModel.Error) or nameof(ColorFieldModel.HasErrors))
            return;

        UpdatePreview();
    }

    private void UpdatePreview()
    {
        _runs = null;
        PreviewColors.Clear();
        PreviewError = "";

        var runs = new List<ColorFieldRun>();
        foreach (var field in Fields)
        {
            field.Error = field.GetErrors().FirstOrDefault()?.ErrorMessage ?? "";
            if (field.TryGetRun(out var run))
                runs.Add(run);
        }

        if (HasErrors)
            PreviewError = GetErrors(nameof(Entries)).First().ErrorMessage ?? "";
        else if (Fields.Count == 0)
            PreviewError = "Add at least one field";
        else if (_palette.DataSource is not { } dataSource)
            PreviewError = "The palette has no data file";
        else if (runs.Count == Fields.Count)
            ShowPreview(runs, dataSource);

        TryAcceptCommand.NotifyCanExecuteChanged();
    }

    private void ShowPreview(List<ColorFieldRun> runs, ImageMagitek.DataSource dataSource)
    {
        try
        {
            var sources = ScatteredColorSource.ExpandRun(Entries, runs);

            for (int n = 0; n < sources.Count; n++)
            {
                var fields = sources[n].Fields;
                for (int k = 0; k < fields.Count; k++)
                {
                    if (fields[k].Offset >= dataSource.Length)
                        throw new ColorFieldException(n, k, "reads past the end of the data file");
                }
            }

            var preview = new Palette(_palette.Name, _colorFactory, _palette.ColorModel, sources.ToList<IColorSource>(), false,
                PaletteStorageSource.ProjectXml, dataSource);

            for (int i = 0; i < preview.Entries; i++)
                PreviewColors.Add(new PaletteSwatchModel(i, ColorRgba32ToMediaColorConverter.ToMediaColor(preview.GetNativeColor(i))));

            _runs = runs;
        }
        catch (ColorFieldException ex)
        {
            PreviewColors.Clear();
            var field = Fields[ex.Field];
            field.Error = ex.Entry is int entry && Entries > 1 ? $"Entry {entry}: {ex.Reason}" : Capitalize(ex.Reason);
        }
        catch (Exception ex)
        {
            PreviewColors.Clear();
            PreviewError = $"Cannot preview these fields: {ex.Message}";
        }
    }

    private static string Capitalize(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];
}
