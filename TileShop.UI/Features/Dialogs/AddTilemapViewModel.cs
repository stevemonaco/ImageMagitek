using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using ImageMagitek;
using ImageMagitek.Codec;
using ImageMagitek.Colors;
using ImageMagitek.Tilemaps;
using TileShop.Shared.Interactions;
using TileShop.Shared.Models;

namespace TileShop.UI.ViewModels;

/// <summary>
/// A data source offered by the dialog, shown by its project path key
/// </summary>
public sealed record TilemapSourceOption(string Key, DataSource Source);

/// <summary>
/// A palette offered for a palette slot, or "(none)" when <see cref="Palette"/> is null
/// </summary>
public sealed record TilemapPaletteOption(string Name, Palette? Palette);

public partial class TilemapPaletteSlotViewModel : ObservableObject
{
    public int Index { get; }
    public IReadOnlyList<TilemapPaletteOption> Options { get; }
    [ObservableProperty] private TilemapPaletteOption _selectedOption;

    public TilemapPaletteSlotViewModel(int index, IReadOnlyList<TilemapPaletteOption> options, TilemapPaletteOption selected)
    {
        Index = index;
        Options = options;
        _selectedOption = selected;
    }
}

public partial class AddTilemapViewModel : RequestViewModel<AddTilemapViewModel>
{
    private readonly Func<string, MagitekResult> _validateName;
    private readonly ICodecFactory _codecFactory;
    private readonly IReadOnlyList<TilemapPaletteOption> _paletteOptions;
    private bool _isInitialized;
    private long _mapOffset;
    private long _tileOffset;

    [ObservableProperty] private string _resourceName = "New Tilemap";
    [ObservableProperty] private string? _nameError;

    public IReadOnlyList<TilemapSourceOption> DataSources { get; }
    public IReadOnlyList<MapFormat> MapFormats { get; }
    public IReadOnlyList<MapScreenLayout> Layouts { get; } = Enum.GetValues<MapScreenLayout>();
    public IReadOnlyList<string> CodecNames { get; }

    [ObservableProperty] private TilemapSourceOption? _selectedMapSource;
    [ObservableProperty] private string _mapOffsetText = "0";
    [ObservableProperty] private MapFormat? _selectedFormat;
    [ObservableProperty] private int _mapWidth = 32;
    [ObservableProperty] private int _mapHeight = 32;
    [ObservableProperty] private MapScreenLayout _selectedLayout = MapScreenLayout.Linear;
    [ObservableProperty] private TilemapSourceOption? _selectedTileSource;
    [ObservableProperty] private string _tileOffsetText = "0";
    [ObservableProperty] private string? _selectedCodecName;
    [ObservableProperty] private int _firstTile;
    [ObservableProperty] private ObservableCollection<TilemapPaletteSlotViewModel> _paletteSlots = [];
    [ObservableProperty] private string _preview = "";
    [ObservableProperty] private bool _isPreviewValid;


    public AddTilemapViewModel(IReadOnlyList<TilemapSourceOption> dataSources, IMapFormatFactory mapFormatFactory,
        IReadOnlyList<string> indexedCodecNames, IReadOnlyList<TilemapPaletteOption> palettes, ICodecFactory codecFactory,
        Func<string, MagitekResult> validateName)
    {
        _validateName = validateName;
        _codecFactory = codecFactory;
        _paletteOptions = [new TilemapPaletteOption("(none)", null), .. palettes];
        Title = "Add New Tilemap";
        AcceptName = "Add";

        DataSources = dataSources;
        MapFormats = mapFormatFactory.Formats;
        CodecNames = indexedCodecNames;

        _selectedMapSource = DataSources.FirstOrDefault();
        _selectedTileSource = DataSources.FirstOrDefault();
        _selectedFormat = MapFormats.FirstOrDefault();
        _selectedCodecName = CodecNames.FirstOrDefault();

        RebuildPaletteSlots();
        ResetSize();
        _isInitialized = true;

        UpdateNameError();
        UpdatePreview();
    }

    partial void OnResourceNameChanged(string value) => UpdateNameError();
    partial void OnSelectedMapSourceChanged(TilemapSourceOption? value) => ResetSizeAndPreview();
    partial void OnMapOffsetTextChanged(string value) => ResetSizeAndPreview();
    partial void OnSelectedFormatChanged(MapFormat? value)
    {
        RebuildPaletteSlots();
        ResetSizeAndPreview();
    }

    partial void OnMapWidthChanged(int value) => UpdatePreview();
    partial void OnMapHeightChanged(int value) => UpdatePreview();
    partial void OnSelectedLayoutChanged(MapScreenLayout value) => UpdatePreview();
    partial void OnSelectedTileSourceChanged(TilemapSourceOption? value) => UpdatePreview();
    partial void OnTileOffsetTextChanged(string value) => UpdatePreview();
    partial void OnSelectedCodecNameChanged(string? value) => UpdatePreview();
    partial void OnFirstTileChanged(int value) => UpdatePreview();

    private void RebuildPaletteSlots()
    {
        foreach (var slot in PaletteSlots)
            slot.PropertyChanged -= OnSlotChanged;

        var count = SelectedFormat?.PaletteSlotCount ?? 1;
        var slots = Enumerable.Range(0, count)
            .Select(i => new TilemapPaletteSlotViewModel(i, _paletteOptions, PaletteSlots.ElementAtOrDefault(i)?.SelectedOption ?? _paletteOptions[0]))
            .ToList();

        foreach (var slot in slots)
            slot.PropertyChanged += OnSlotChanged;

        PaletteSlots = new(slots);
    }

    private void OnSlotChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) => UpdatePreview();

    private void ResetSizeAndPreview()
    {
        ResetSize();
        UpdatePreview();
    }

    private void ResetSize()
    {
        if (SelectedMapSource is null || SelectedFormat is null || !JumpToOffsetViewModel.TryParseOffset(MapOffsetText, NumericBase.Hexadecimal, out var offset))
            return;

        long length;
        try
        {
            length = SelectedMapSource.Source.Length;
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
        {
            return;
        }

        var entries = Math.Max(0, length - offset) / SelectedFormat.EntryBytes;
        var wasInitialized = _isInitialized;
        _isInitialized = false;
        MapWidth = 32;
        MapHeight = (int)Math.Clamp(entries / 32, 1, 1024);
        _isInitialized = wasInitialized;
    }

    private void UpdateNameError()
    {
        var result = _validateName(ResourceName);
        NameError = result.HasFailed ? result.AsError.Reason : null;
        TryAcceptCommand.NotifyCanExecuteChanged();
    }

    private void UpdatePreview()
    {
        if (!_isInitialized)
            return;

        IsPreviewValid = false;
        Preview = FindInputError() ?? DescribeTilemap();
        TryAcceptCommand.NotifyCanExecuteChanged();
    }

    private string? FindInputError()
    {
        if (SelectedMapSource is null || SelectedTileSource is null)
            return "The project has no data files";

        if (!JumpToOffsetViewModel.TryParseOffset(MapOffsetText, NumericBase.Hexadecimal, out var mapOffset))
            return "Enter the map offset in hexadecimal";

        if (!JumpToOffsetViewModel.TryParseOffset(TileOffsetText, NumericBase.Hexadecimal, out var tileOffset))
            return "Enter the tile offset in hexadecimal";

        if (SelectedFormat is null)
            return "Choose a map format";

        if (SelectedCodecName is null)
            return "Choose a tile codec";

        if (MapWidth <= 0 || MapHeight <= 0 || FirstTile < 0)
            return "Enter a positive width and height and a first tile index of 0 or more";

        _mapOffset = mapOffset;
        _tileOffset = tileOffset;
        return null;
    }

    private string DescribeTilemap()
    {
        try
        {
            using var tilemap = CreateTilemap("preview");

            if (tilemap.MapError is { } error)
                return error;

            IsPreviewValid = true;
            var description = $"{MapWidth * MapHeight} entries, highest tile index {tilemap.HighestTileIndex}";
            return tilemap.OutOfRangeCount > 0
                ? $"{description}; {tilemap.OutOfRangeCount} entries are outside the tile set and show as empty cells"
                : description;
        }
        catch (ArgumentException ex)
        {
            return ex.Message;
        }
    }

    /// <exception cref="ArgumentException">The inputs do not describe a valid tilemap</exception>
    public TilemapArranger CreateTilemap(string name) =>
        new(name, SelectedMapSource!.Source, _mapOffset, SelectedFormat!, SelectedLayout, MapWidth, MapHeight,
            SelectedTileSource!.Source, _tileOffset, SelectedCodecName!, FirstTile,
            PaletteSlots.Select(x => x.SelectedOption.Palette).ToList(), _codecFactory);

    protected override bool CanAccept() => NameError is null && IsPreviewValid;

    public override AddTilemapViewModel? ProduceResult() => this;
}
