using System;
using System.Collections.Generic;
using System.Linq;
using TileShop.Shared.Messages;
using ImageMagitek.Colors;
using ImageMagitek.Services;
using ImageMagitek.Utility.Parsing;
using ImageMagitek;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Input;
using System.Threading.Tasks;
using TileShop.Shared.Models;
using TileShop.UI.Models;

namespace TileShop.UI.ViewModels;

public partial class PaletteEditorViewModel : ResourceEditorBaseViewModel
{
    private const int _maxColumns = 16;

    protected readonly Palette _palette;
    protected readonly IColorFactory _colorFactory;
    protected readonly IProjectService _projectService;

    public ObservableCollection<PaletteSwatchModel> Colors { get; } = [];
    [ObservableProperty] private ObservableCollection<ColorSourceModel> _colorSourceModels = new();
    [ObservableProperty] private string _paletteSource;
    [ObservableProperty] private int _entries;
    [ObservableProperty] private ColorModel _colorModel;
    [ObservableProperty] private EditableColorBaseViewModel? _activeColor;

    [ObservableProperty] private int _columns;
    [ObservableProperty] private double _cellSize;
    [ObservableProperty] private IReadOnlyList<string> _columnHeaders = [];
    [ObservableProperty] private IReadOnlyList<string> _rowHeaders = [];
    public bool HasMultipleRows => RowHeaders.Count > 1;

    public bool IsReadOnly => _palette.StorageSource == PaletteStorageSource.GlobalJson;

    private bool _zeroIndexTransparent;
    public bool ZeroIndexTransparent
    {
        get => _zeroIndexTransparent;
        set
        {
            if (SetProperty(ref _zeroIndexTransparent, value))
                IsModified = true;
        }
    }

    private int _selectedColorIndex;
    public int SelectedColorIndex
    {
        get => _selectedColorIndex;
        set
        {
            if (SetProperty(ref _selectedColorIndex, value))
                RefreshSelection();
        }
    }

    public PaletteEditorViewModel(Palette palette, IColorFactory colorFactory, IProjectService projectService) : base(palette)
    {
        _palette = palette;
        _colorFactory = colorFactory;
        _projectService = projectService;

        DisplayName = Resource?.Name ?? "Unnamed Palette";

        _zeroIndexTransparent = _palette.ZeroIndexTransparent;
        ColorModel = _palette.ColorModel;
        ColorSourceModels = new(CreateColorSourceModels(_palette));
        _paletteSource = DescribeSource();
        Entries = CountSourceColors();

        RebuildSwatches();
    }

    [RelayCommand]
    private void SelectColor(PaletteSwatchModel swatch) => SelectedColorIndex = swatch.Index;

    /// <summary>
    /// Saves color sources to their project resource
    /// </summary>
    [RelayCommand]
    public async Task SaveSources()
    {
        _palette.ZeroIndexTransparent = ZeroIndexTransparent;

        _palette.SetColorSources(CreateColorSources());
        var projectTree = _projectService.GetContainingProject(_palette);
        var paletteNode = projectTree.GetResourceNode(_palette);
        await _projectService.SaveResourceAsync(projectTree, paletteNode, false);

        Entries = CountSourceColors();
        PaletteSource = DescribeSource();
        RebuildSwatches();

        var changeMessage = new PaletteChangedMessage(_palette);
        Messenger.Send(changeMessage);

        IsModified = false;
    }

    [RelayCommand]
    public async Task SaveActiveColor()
    {
        if (ActiveColor is null)
            return;

        _palette.SetForeignColor(ActiveColor.Index, ActiveColor.WorkingColor);
        ActiveColor.SaveColor();
        Colors[ActiveColor.Index].Color = ActiveColor.Color;

        await SaveChangesAsync();
    }

    /// <summary>
    /// Saves palette properties and color source values to their underlying sources
    /// </summary>
    [RelayCommand]
    public override async Task SaveChangesAsync()
    {
        _palette.ZeroIndexTransparent = ZeroIndexTransparent;

        var projectTree = _projectService.GetContainingProject(_palette);
        var paletteNode = projectTree.GetResourceNode(_palette);
        await _projectService.SaveResourceAsync(projectTree, paletteNode, false);
        _palette.SavePalette();
        IsModified = false;

        var changeMessage = new PaletteChangedMessage(_palette);
        Messenger.Send(changeMessage);
    }

    public override void DiscardChanges()
    {
        _palette.Reload();
        ZeroIndexTransparent = _palette.ZeroIndexTransparent;
        ColorSourceModels = new(CreateColorSourceModels(_palette));
        Entries = CountSourceColors();
        IsModified = false;
    }

    public override void Undo()
    {
        throw new NotImplementedException();
    }

    public override void Redo()
    {
        throw new NotImplementedException();
    }

    public override void ApplyHistoryAction(HistoryAction action)
    {
        throw new NotImplementedException();
    }

    private string DescribeSource()
    {
        if (_palette.DataSource is not null)
            return _palette.DataSource.Name;
        else if (_palette.StorageSource == PaletteStorageSource.GlobalJson)
            return "Built-in";
        else
            return "Unknown";
    }

    /// <summary>
    /// Reloads the swatch grid from the palette, keeping the selection where it is still valid
    /// </summary>
    private void RebuildSwatches()
    {
        Colors.Clear();

        for (int i = 0; i < _palette.Entries; i++)
        {
            var native = _palette.GetNativeColor(i);
            Colors.Add(new PaletteSwatchModel(i, Avalonia.Media.Color.FromArgb(native.A, native.R, native.G, native.B)));
        }

        Columns = Math.Clamp(Colors.Count, 1, _maxColumns);
        CellSize = Colors.Count switch
        {
            <= 16 => 40,
            <= 64 => 32,
            _ => 24
        };
        ColumnHeaders = Enumerable.Range(0, Columns).Select(x => x.ToString()).ToList();
        RowHeaders = Enumerable.Range(0, (Colors.Count + Columns - 1) / Columns).Select(x => (x * Columns).ToString()).ToList();
        OnPropertyChanged(nameof(HasMultipleRows));

        _selectedColorIndex = Math.Clamp(_selectedColorIndex, Colors.Count > 0 ? 0 : -1, Colors.Count - 1);
        OnPropertyChanged(nameof(SelectedColorIndex));
        RefreshSelection();
    }

    private void RefreshSelection()
    {
        foreach (var swatch in Colors)
            swatch.IsSelected = swatch.Index == _selectedColorIndex;

        ActiveColor = _selectedColorIndex >= 0 && _selectedColorIndex < Colors.Count
            ? CreateActiveColorEditor(_palette.GetForeignColor(_selectedColorIndex), _selectedColorIndex)
            : null;
    }

    private EditableColorBaseViewModel CreateActiveColorEditor(IColor foreignColor, int index)
    {
        EditableColorBaseViewModel editor = foreignColor switch
        {
            IColor32 color32 => new Color32ViewModel(color32, index, _colorFactory, _palette.ColorModel),
            ITableColor tableColor => new TableColorViewModel(tableColor, index, _colorFactory, _palette.ColorModel),
            _ => throw new NotSupportedException($"Color of type '{foreignColor.GetType()}' is not supported for editing")
        };

        editor.SaveColorCommand = SaveActiveColorCommand;
        editor.IsReadOnly = IsReadOnly;
        return editor;
    }

    [RelayCommand]
    public void AddNewFileColorSource()
    {
        ColorSourceModels.Add(new FileColorSourceModel(0, 0, Endian.Little));
        Entries = CountSourceColors();
    }

    [RelayCommand]
    public void AddNewNativeColorSource()
    {
        var color = _colorFactory.CreateColor(ColorModel.Rgba32, 0, 0, 0, 255);
        var hexString = _colorFactory.ToHexString(color);
        ColorSourceModels.Add(new NativeColorSourceModel(hexString));
        Entries = CountSourceColors();
    }

    [RelayCommand]
    public void AddNewForeignColorSource()
    {
        var color = _colorFactory.CreateColor(_palette.ColorModel, 0);
        var hexString = _colorFactory.ToHexString(color);
        ColorSourceModels.Add(new ForeignColorSourceModel(hexString));
        Entries = CountSourceColors();
    }

    [RelayCommand]
    public void RemoveColorSource(ColorSourceModel model)
    {
        ColorSourceModels.Remove(model);
        Entries = CountSourceColors();
    }

    private IEnumerable<ColorSourceModel> CreateColorSourceModels(Palette pal)
    {
        var size = _colorFactory.CreateColor(pal.ColorModel).Size;

        int i = 0;
        while (i < pal.ColorSources.Length)
        {
            if (pal.ColorSources[i] is FileColorSource fileSource)
            {
                var sources = pal.ColorSources.Skip(i)
                    .TakeWhile((x, i) => x is FileColorSource source && source.Offset == (fileSource.Offset + i * size))
                    .ToList();

                var fileSourceModel = new FileColorSourceModel(fileSource.Offset.ByteOffset, sources.Count, fileSource.Endian);
                yield return fileSourceModel;

                i += sources.Count;
            }
            else if (pal.ColorSources[i] is ProjectNativeColorSource nativeSource)
            {
                var hexString = _colorFactory.ToHexString(nativeSource.Value);
                var nativeSourceModel = new NativeColorSourceModel(hexString);
                yield return nativeSourceModel;
                i++;
            }
            else if (pal.ColorSources[i] is ProjectForeignColorSource foreignSource)
            {
                var hexString = _colorFactory.ToHexString(foreignSource.Value);
                var foreignSourceModel = new ForeignColorSourceModel(hexString);
                yield return foreignSourceModel;
                i++;
            }
            else if (pal.ColorSources[i] is ScatteredColorSource scatteredSource)
            {
            }
        }
    }

    private int CountSourceColors()
    {
        int count = 0;

        foreach (var source in ColorSourceModels)
        {
            count += source switch
            {
                FileColorSourceModel fileSource => fileSource.Entries,
                NativeColorSourceModel => 1,
                ForeignColorSourceModel => 1,
                _ => 0
            };
        }

        return count;
    }

    private IEnumerable<IColorSource> CreateColorSources()
    {
        var size = _colorFactory.CreateColor(_palette.ColorModel).Size;

        for (int i = 0; i < ColorSourceModels.Count; i++)
        {
            var sourceModel = ColorSourceModels[i];
            if (sourceModel is FileColorSourceModel fileModel)
            {
                var offset = new BitAddress(fileModel.FileAddress, 0);
                for (int j = 0; j < fileModel.Entries; j++)
                    yield return new FileColorSource(offset + j * size, fileModel.Endian);
            }
            else if (sourceModel is NativeColorSourceModel nativeModel)
            {
                if (ColorParser.TryParse(nativeModel.NativeHexColor, ColorModel.Rgba32, out var nativeColor))
                    yield return new ProjectNativeColorSource((ColorRgba32)nativeColor);
            }
            else if (sourceModel is ForeignColorSourceModel foreignModel)
            {
                if (ColorParser.TryParse(foreignModel.ForeignHexColor, _palette.ColorModel, out var foreignColor))
                    yield return new ProjectForeignColorSource(foreignColor);
            }
            else if (sourceModel is ScatteredColorSourceModel)
            {
                throw new NotSupportedException();
            }
        }
    }
}
