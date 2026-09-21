using System;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageMagitek;
using ImageMagitek.Codec;
using ImageMagitek.Colors;
using ImageMagitek.Image.Import;
using TileShop.Shared.Interactions;
using TileShop.Shared.Models;
using TileShop.Shared.Services;
using TileShop.UI.Models;

namespace TileShop.UI.ViewModels;

/// <summary>
/// Stages an image file for import into an arranger, previewing the outcome of the chosen color matching before anything is written
/// </summary>
public partial class ImportImageViewModel : RequestViewModel<ImportImageViewModel>
{
    private readonly IAsyncFileRequestService _fileSelect;
    private readonly UserPreferencesStore _preferencesStore;
    private readonly IImageFileAdapter _fileAdapter = new ImageSharpFileAdapter();
    private DecodedImage? _source;

    public override DialogSize Size => DialogSize.Large;

    public Arranger Arranger { get; }
    public bool IsIndexed => Arranger.ColorType == PixelColorType.Indexed;
    public GridSettingsViewModel GridSettings { get; }

    /// <summary>Unmatched colors first since they block the import, then substitutions farthest first</summary>
    public ObservableCollection<ImportColorEntryViewModel> Entries { get; } = [];

    [ObservableProperty] private string? _imageFileName;
    [ObservableProperty] private string? _imageDescription;
    [ObservableProperty] private string? _importError;
    [ObservableProperty] private string? _summary;
    [ObservableProperty] private ImageImportPreview? _preview;

    [ObservableProperty] private ColorMatchStrategy _matchStrategy;
    [ObservableProperty] private bool _mapTransparentToIndexZero;

    [ObservableProperty] private ImportPreviewMode _previewMode;
    [ObservableProperty] private double _onionSkinOpacity;
    [ObservableProperty] private double _zoom = 1;
    [ObservableProperty] private ImportColorEntryViewModel? _selectedEntry;
    [ObservableProperty] private string? _hoverDescription;

    public bool IsOnionSkin => PreviewMode == ImportPreviewMode.OnionSkin;
    public bool HasEntries => Entries.Count > 0;
    public bool HasPreview => Preview is not null;
    public string ZoomDescription => $"{Zoom * 100:0}%";

    /// <summary>Why the import cannot proceed, or null when it can</summary>
    public string? BlockingReason => ImportError ?? (Preview is { CanCommit: false } preview
        ? $"{preview.Report.Unmatched.Count} {(preview.Report.Unmatched.Count == 1 ? "color" : "colors")} could not be matched exactly — choose Nearest matching or fix the image"
        : null);

    public Action? OnPreviewReplaced { get; set; }
    public Action? OnInvalidated { get; set; }
    public Action? OnZoomIn { get; set; }
    public Action? OnZoomOut { get; set; }
    public Action? OnFitToViewport { get; set; }
    public Action? OnResetZoom { get; set; }
    public Action<Point>? OnCenterOn { get; set; }

    public ImportImageViewModel(Arranger arranger, string imageFileName, IAsyncFileRequestService fileSelect, UserPreferencesStore preferencesStore)
    {
        if (arranger.ColorType is not (PixelColorType.Indexed or PixelColorType.Direct))
            throw new ArgumentException($"Invalid color type for '{arranger.Name}': {arranger.ColorType}");

        Arranger = arranger;
        _fileSelect = fileSelect;
        _preferencesStore = preferencesStore;

        var preferences = preferencesStore.Preferences;
        GridSettings = GridSettingsViewModel.CreateDefault(arranger, preferences.Grid);
        _matchStrategy = preferences.ImportImage.MatchStrategy;
        _mapTransparentToIndexZero = preferences.ImportImage.MapTransparentToIndexZero ?? HasTransparentZeroIndex();
        _previewMode = preferences.ImportImage.PreviewMode;
        _onionSkinOpacity = preferences.ImportImage.OnionSkinOpacity;

        Title = $"Import Image Into '{arranger.Name}'";
        AcceptName = "Import";

        Load(imageFileName);
    }

    private bool HasTransparentZeroIndex() =>
        Arranger.EnumerateElements().Any(x => x?.Codec is IIndexedCodec { Palette.ZeroIndexTransparent: true });

    [RelayCommand]
    private async Task BrowseForImportFile()
    {
        var fileName = await _fileSelect.RequestImportArrangerFileName();

        if (fileName is not null)
            Load(fileName.LocalPath);
    }

    [RelayCommand] private void ZoomIn() => OnZoomIn?.Invoke();
    [RelayCommand] private void ZoomOut() => OnZoomOut?.Invoke();
    [RelayCommand] private void FitToViewport() => OnFitToViewport?.Invoke();
    [RelayCommand] private void ResetZoom() => OnResetZoom?.Invoke();

    [RelayCommand]
    private void LocateEntry(ImportColorEntryViewModel entry)
    {
        SelectedEntry = entry;
        OnCenterOn?.Invoke(entry.FirstLocation);
    }

    public void Load(string fileName)
    {
        ImageFileName = fileName;

        _fileAdapter.LoadImage(fileName).Switch(
            success =>
            {
                _source = success.Result;
                ImageDescription = $"{_source.Width}×{_source.Height}";
                Reimport();
            },
            fail =>
            {
                _source = null;
                ImageDescription = null;
                ImportError = fail.Reason;
                SetPreview(null);
            });
    }

    private void Reimport()
    {
        if (_source is null)
            return;

        var options = new ImageImportOptions(MatchStrategy, MapTransparentToIndexZero);

        ImageImporter.Prepare(Arranger, _source, options).Switch(
            success =>
            {
                ImportError = null;
                SetPreview(success.Result);
            },
            fail =>
            {
                ImportError = fail.Reason;
                SetPreview(null);
            });
    }

    private void SetPreview(ImageImportPreview? preview)
    {
        SelectedEntry = null;
        Entries.Clear();

        if (preview is not null)
        {
            foreach (var unmatched in preview.Report.Unmatched)
                Entries.Add(new ImportColorEntryViewModel(unmatched));

            foreach (var substitution in preview.Report.Substitutions)
                Entries.Add(new ImportColorEntryViewModel(substitution));
        }

        Summary = preview?.Report.ToSummary();
        OnPropertyChanged(nameof(HasEntries));
        Preview = preview;
    }

    /// <summary>
    /// Describes the pixel under the pointer, or clears the description when it is off the image
    /// </summary>
    public void UpdateHover(int x, int y)
    {
        if (Preview is not { } preview || x < 0 || y < 0 || x >= preview.Source.Width || y >= preview.Source.Height)
        {
            HoverDescription = null;
            return;
        }

        var i = y * preview.Source.Width + x;
        var source = preview.Source.Pixels[i];
        var state = preview.Report.PixelStates[i];

        var target = preview.ResultIndexed is { } indexed ? $"index {indexed.Image[i]}" : ToHex(preview.ResultDirect!.Image[i]);
        var stateText = state.HasFlag(ImportPixelState.Unmatched) ? " · unmatched"
            : state.HasFlag(ImportPixelState.Substituted) ? " · substituted"
            : state.HasFlag(ImportPixelState.Changed) ? " · changed"
            : "";

        HoverDescription = $"({x}, {y})  {ToHex(source)} → {target}{stateText}";
    }

    private static string ToHex(ColorRgba32 color) =>
        color.A == 255 ? $"#{color.R:X2}{color.G:X2}{color.B:X2}" : $"#{color.R:X2}{color.G:X2}{color.B:X2}{color.A:X2}";

    partial void OnMatchStrategyChanged(ColorMatchStrategy value) => Reimport();
    partial void OnMapTransparentToIndexZeroChanged(bool value) => Reimport();
    partial void OnOnionSkinOpacityChanged(double value) => OnInvalidated?.Invoke();
    partial void OnSelectedEntryChanged(ImportColorEntryViewModel? value) => OnInvalidated?.Invoke();
    partial void OnZoomChanged(double value) => OnPropertyChanged(nameof(ZoomDescription));
    partial void OnImportErrorChanged(string? value) => OnPropertyChanged(nameof(BlockingReason));

    partial void OnPreviewModeChanged(ImportPreviewMode value)
    {
        OnPropertyChanged(nameof(IsOnionSkin));
        OnInvalidated?.Invoke();
    }

    partial void OnPreviewChanged(ImageImportPreview? value)
    {
        OnPropertyChanged(nameof(HasPreview));
        OnPropertyChanged(nameof(BlockingReason));
        TryAcceptCommand.NotifyCanExecuteChanged();
        OnPreviewReplaced?.Invoke();
    }

    public override ImportImageViewModel? ProduceResult() => this;

    protected override bool CanAccept() => Preview?.CanCommit == true;

    protected override Task<bool> OnAccepted()
    {
        if (Preview is not { CanCommit: true } preview)
            return Task.FromResult(false);

        preview.Commit();

        _preferencesStore.Preferences.ImportImage = new ImportImagePreferences(MatchStrategy, MapTransparentToIndexZero, PreviewMode, OnionSkinOpacity);
        _preferencesStore.Save();

        return Task.FromResult(true);
    }
}
