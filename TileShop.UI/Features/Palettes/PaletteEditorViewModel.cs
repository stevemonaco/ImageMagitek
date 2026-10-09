using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using ImageMagitek.Colors;
using ImageMagitek.Colors.Serialization;
using ImageMagitek.Project;
using static ImageMagitek.Project.Serialization.SerializationMapperExtensions;
using ImageMagitek.Services;
using ImageMagitek.Utility.Parsing;
using ImageMagitek;
using System.Collections.ObjectModel;
using Avalonia.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Threading.Tasks;
using TileShop.Shared.Interactions;
using TileShop.Shared.Models;
using TileShop.UI.Converters;
using TileShop.UI.Features.Palettes;
using TileShop.UI.Models;
using TileShop.UI.Services;

namespace TileShop.UI.ViewModels;

public partial class PaletteEditorViewModel : ResourceEditorBaseViewModel
{
    private const int _maxColumns = 16;

    private readonly Palette _palette;
    private readonly IColorFactory _colorFactory;
    private readonly IProjectService _projectService;
    private readonly IInteractionService _interactions;
    private readonly IAsyncFileRequestService _fileRequests;
    private readonly ClipboardService _clipboard;
    private readonly PaletteSelection _selection = new();
    private readonly PaletteEditSession _session;

    private bool _isApplyingSourceEdit;
    private bool _isProjectSavePending;
    private bool _hasOutOfRangeSources;
    private KeyModifiers _clickModifiers;
    private IColor[]? _copiedColors;
    private string? _copiedText;

    public ObservableCollection<PaletteSwatchModel> Colors { get; } = [];
    [ObservableProperty] private ObservableCollection<ColorSourceModel> _colorSourceModels = new();
    [ObservableProperty] private string _paletteSource;
    [ObservableProperty] private int _entries;
    [ObservableProperty] private ColorModel _colorModel;
    [ObservableProperty] private EditableColorBaseViewModel? _activeColor;
    [ObservableProperty] private string _selectedColorOffset = "";
    [ObservableProperty] private string _selectionSummary = "";

    [ObservableProperty] private int _columns;
    [ObservableProperty] private double _cellSize;
    [ObservableProperty] private IReadOnlyList<string> _columnHeaders = [];
    [ObservableProperty] private IReadOnlyList<string> _rowHeaders = [];
    public bool HasMultipleRows => RowHeaders.Count > 1;

    // Fixed at open so a palette that turns read-only through Sources edits reaches Save's commit-failure alert
    public bool IsReadOnly { get; }

    public bool ZeroIndexTransparent
    {
        get => _palette.ZeroIndexTransparent;
        set
        {
            if (!IsReadOnly)
                _session.SetZeroIndexTransparent(value);
        }
    }

    public override IReadOnlyList<Hotkey> Hotkeys => field ??=
    [
        new("Ctrl+S", SaveChangesCommand),
        new("Ctrl+Z", UndoCommand),
        new("Ctrl+Y", RedoCommand),
        new("Ctrl+C", CopyCommand),
        new("Ctrl+V", PasteCommand),
        new("Ctrl+A", SelectAllCommand),
        new("Left", MoveSelectionCommand, "Left"),
        new("Right", MoveSelectionCommand, "Right"),
        new("Up", MoveSelectionCommand, "Up"),
        new("Down", MoveSelectionCommand, "Down"),
        new("Shift+Left", ExtendSelectionCommand, "Left"),
        new("Shift+Right", ExtendSelectionCommand, "Right"),
        new("Shift+Up", ExtendSelectionCommand, "Up"),
        new("Shift+Down", ExtendSelectionCommand, "Down"),
    ];

    public override EditCommands EditCommands => field ??= new(UndoCommand, RedoCommand, EditCommands.Disabled, CopyCommand, PasteCommand, EditCommands.Disabled, SelectAllCommand);

    public PaletteEditorViewModel(Palette palette, IColorFactory colorFactory, IProjectService projectService,
        IInteractionService interactions, IAsyncFileRequestService fileRequests, ClipboardService clipboard) : base(palette)
    {
        _palette = palette;
        IsReadOnly = palette.IsReadOnly;
        _colorFactory = colorFactory;
        _projectService = projectService;
        _interactions = interactions;
        _fileRequests = fileRequests;
        _clipboard = clipboard;

        _session = new PaletteEditSession(palette, colorFactory, UndoHistory, RedoHistory);
        _session.Changed += OnSessionChanged;

        DisplayName = Resource?.Name ?? "Unnamed Palette";

        ColorModel = _palette.ColorModel;
        _paletteSource = DescribeSource();
        RebuildSourceModels();
        Entries = CountSourceColors();

        if (_palette.Entries > 0)
            _selection.Click(0);

        RebuildSwatches();
    }

    /// <summary>
    /// Assigns a color as a pending edit, such as one confirmed in a graphics editor's color flyout
    /// </summary>
    public void AssignColor(int index, IColor color)
    {
        if (!IsReadOnly && index >= 0 && index < _palette.Entries)
            _session.SetColor(index, color);
    }

    /// <summary>
    /// Records the modifier keys of the pointer release that is about to click a swatch
    /// </summary>
    public void SetClickModifiers(KeyModifiers modifiers) => _clickModifiers = modifiers;

    [RelayCommand]
    private void SelectColor(PaletteSwatchModel swatch)
    {
        var modifiers = _clickModifiers;
        _clickModifiers = KeyModifiers.None;

        if (modifiers.HasFlag(KeyModifiers.Shift))
            _selection.ShiftClick(swatch.Index);
        else if (modifiers.HasFlag(KeyModifiers.Control) || modifiers.HasFlag(KeyModifiers.Meta))
            _selection.CtrlClick(swatch.Index);
        else
            _selection.Click(swatch.Index);

        RefreshSelection();
    }

    [RelayCommand]
    private void MoveSelection(string direction) => MoveFocus(direction, false);

    [RelayCommand]
    private void ExtendSelection(string direction) => MoveFocus(direction, true);

    private void MoveFocus(string direction, bool extend)
    {
        var (dx, dy) = direction switch
        {
            "Left" => (-1, 0),
            "Right" => (1, 0),
            "Up" => (0, -1),
            "Down" => (0, 1),
            _ => (0, 0)
        };

        _selection.Move(dx, dy, Columns, Colors.Count, extend);
        RefreshSelection();
    }

    [RelayCommand]
    private void SelectAll()
    {
        if (Colors.Count == 0)
            return;

        _selection.Click(0);
        _selection.ShiftClick(Colors.Count - 1);
        RefreshSelection();
    }

    [RelayCommand]
    private void AssignActiveColor()
    {
        if (ActiveColor is null || IsReadOnly)
            return;

        _session.SetColor(ActiveColor.Index, ActiveColor.WorkingColor);
    }

    private bool HasSelection() => _selection.Count > 0;
    private bool CanEditSelection() => !IsReadOnly && _selection.Count > 0;
    private bool CanSwap() => !IsReadOnly && _selection.Count == 2;
    private bool CanFillGradient() => !IsReadOnly && _selection.Count >= 3;

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task Copy()
    {
        var indices = _selection.ToSortedList();
        _copiedColors = indices.Select(i => _colorFactory.CloneColor(_palette.GetForeignColor(i))).ToArray();
        _copiedText = string.Join(Environment.NewLine, indices.Select(i => ColorRgba32ToMediaColorConverter.ToHex(_palette.GetNativeColor(i))));

        await _clipboard.SetTextAsync(_copiedText);
    }

    /// <summary>
    /// Pastes from the first selected color on. Colors copied in TileShop paste exactly; other clipboard text is read as native hex lines.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanEditSelection))]
    private async Task Paste()
    {
        var text = await _clipboard.GetTextAsync();
        IReadOnlyList<IColor>? colors;

        if (_copiedColors is not null && (text is null || NormalizeLines(text) == NormalizeLines(_copiedText)))
            colors = _copiedColors;
        else if (text is null || !TryParseColors(text, out colors))
        {
            ActivityMessage = "The clipboard does not contain colors as #RRGGBB or #RRGGBBAA lines";
            return;
        }

        _session.SetColors(_selection.First, colors);
    }

    [RelayCommand(CanExecute = nameof(CanSwap))]
    private void Swap()
    {
        var indices = _selection.ToSortedList();
        _session.SwapColors(indices[0], indices[1]);
    }

    [RelayCommand(CanExecute = nameof(CanFillGradient))]
    private void Gradient() => _session.FillGradient(_selection.ToSortedList());

    [RelayCommand]
    private async Task ImportPalette()
    {
        var uri = await _fileRequests.RequestImportPaletteFileName();
        if (uri is null)
            return;

        try
        {
            var text = await File.ReadAllTextAsync(uri.LocalPath);
            var result = PaletteFileSerializer.Read(text);

            if (result.HasFailed)
            {
                await _interactions.AlertAsync("Import Error", $"Could not read '{Path.GetFileName(uri.LocalPath)}'\n{result.AsError.Reason}");
                return;
            }

            var sources = result.AsSuccess.Result.Select(x => (IColorSource)new ProjectNativeColorSource(x)).ToList();
            _session.SetSources(sources, "Import palette");
        }
        catch (Exception ex)
        {
            await _interactions.AlertAsync("Import Error", ex.Message);
        }
    }

    [RelayCommand]
    private async Task ExportPalette()
    {
        var uri = await _fileRequests.RequestExportPaletteFileName($"{_palette.Name}.pal");
        if (uri is null)
            return;

        try
        {
            var path = uri.LocalPath;
            var colors = Enumerable.Range(0, _palette.Entries).Select(_palette.GetNativeColor).ToList();
            var text = string.Equals(Path.GetExtension(path), ".gpl", StringComparison.OrdinalIgnoreCase)
                ? PaletteFileSerializer.WriteGpl(_palette.Name, colors)
                : PaletteFileSerializer.WriteJasc(colors);

            await File.WriteAllTextAsync(path, text);
        }
        catch (Exception ex)
        {
            await _interactions.AlertAsync("Export Error", ex.Message);
        }
    }

    [RelayCommand]
    private async Task ChangeColorModel()
    {
        if (HasInvalidSources())
        {
            await _interactions.AlertAsync("Invalid Sources", "Fix the sources marked as invalid before changing the color model.");
            return;
        }

        var dialog = new ChangeColorModelViewModel(_palette, _colorFactory);
        var result = await _interactions.RequestAsync(dialog);

        if (result is not null)
            _session.ChangeColorModel(result.Model, result.Sources);
    }

    /// <summary>
    /// Writes pending colors to the data source and the palette's sources to the project
    /// </summary>
    [RelayCommand]
    public override async Task SaveChangesAsync()
    {
        if (IsReadOnly)
            return;

        if (HasInvalidSources())
        {
            await _interactions.AlertAsync("Invalid Sources", $"'{DisplayName}' has sources marked as invalid. Fix them before saving.");
            return;
        }

        try
        {
            if (_session.IsModified && !_session.Commit())
            {
                await _interactions.AlertAsync("Save Error", $"'{DisplayName}' cannot be written to its data source");
                return;
            }

            _isProjectSavePending = true;
            var projectTree = _projectService.GetContainingProject(_palette);

            if (projectTree.TryFindResourceNode(_palette, out var paletteNode))
            {
                var result = await _projectService.SaveResourceAsync(projectTree, paletteNode, false);

                if (result.HasFailed)
                    await _interactions.AlertAsync("Project Error", $"An error occurred while saving: {result.AsError.Reason}");
                else
                    _isProjectSavePending = false;
            }
        }
        catch (Exception ex)
        {
            await _interactions.AlertAsync("Save Error", $"Could not save the palette\n{ex.Message}");
        }

        UpdateModified();
    }

    public override void DiscardChanges()
    {
        _isProjectSavePending = false;
        _session.Discard();
    }

    /// <summary>
    /// Discards pending edits and rewrites the palette's project entry, since a whole-project save may have written them
    /// </summary>
    public async Task DiscardChangesAsync()
    {
        DiscardChanges();

        if (IsReadOnly)
            return;

        var projectTree = _projectService.GetContainingProject(_palette);
        if (projectTree.TryFindResourceNode(_palette, out var paletteNode))
        {
            var result = await _projectService.SaveResourceAsync(projectTree, paletteNode, false);
            if (result.HasFailed)
                await _interactions.AlertAsync("Project Error", $"Could not restore the saved palette: {result.AsError.Reason}");
        }
    }

    [RelayCommand(CanExecute = nameof(CanUndo))]
    public override void Undo() => _session.Undo();

    [RelayCommand(CanExecute = nameof(CanRedo))]
    public override void Redo() => _session.Redo();

    public override void ApplyHistoryAction(HistoryAction action)
    {
        throw new NotImplementedException();
    }

    private void OnSessionChanged(object? sender, EventArgs e)
    {
        ColorModel = _palette.ColorModel;
        OnPropertyChanged(nameof(ZeroIndexTransparent));

        if (!_isApplyingSourceEdit)
            RebuildSourceModels();

        Entries = CountSourceColors();
        RebuildSwatches();

        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
        UpdateModified();
        UpdateCommittedModel();
    }

    private void UpdateCommittedModel()
    {
        if (IsReadOnly)
            return;

        if (_projectService.FindContainingProject(_palette)?.TryFindResourceNode(_palette, out var node) != true
            || node is not PaletteNode paletteNode)
            return;

        if (_session.IsModified)
        {
            var saved = _session.SavedState;
            paletteNode.CommittedModel = map => _palette.MapToModel(map, _colorFactory, saved.ColorModel, saved.ZeroIndexTransparent, saved.Sources);
        }
        else
        {
            paletteNode.CommittedModel = null;
        }
    }

    private void UpdateModified() => IsModified = _session.IsModified || _isProjectSavePending || HasInvalidSources();

    private bool HasInvalidSources() => _hasOutOfRangeSources || ColorSourceModels.Any(x => x.HasErrors);

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
        if (Colors.Count == _palette.Entries)
        {
            for (int i = 0; i < Colors.Count; i++)
                Colors[i].Color = ColorRgba32ToMediaColorConverter.ToMediaColor(_palette.GetNativeColor(i));
        }
        else
        {
            Colors.Clear();
            for (int i = 0; i < _palette.Entries; i++)
                Colors.Add(new PaletteSwatchModel(i, ColorRgba32ToMediaColorConverter.ToMediaColor(_palette.GetNativeColor(i))));
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

        _selection.Clamp(Colors.Count);
        RefreshSelection();
    }

    private void RefreshSelection()
    {
        foreach (var swatch in Colors)
            swatch.IsSelected = _selection.Contains(swatch.Index);

        var activeIndex = _selection.Contains(_selection.Focus) ? _selection.Focus : _selection.First;

        if (activeIndex < 0 || activeIndex >= _palette.Entries)
            ActiveColor = null;
        else if (ActiveColor?.Index != activeIndex || !ActiveColor.IsEditing(_palette.GetForeignColor(activeIndex)))
            ActiveColor = CreateActiveColorEditor(_palette.GetForeignColor(activeIndex), activeIndex);

        SelectionSummary = _selection.Count switch
        {
            0 => "",
            1 => $"Color {activeIndex}",
            _ => $"Color {activeIndex} · {_selection.Count} selected"
        };
        SelectedColorOffset = DescribeColorSource(activeIndex);
        UpdateSourceHighlights();

        CopyCommand.NotifyCanExecuteChanged();
        PasteCommand.NotifyCanExecuteChanged();
        SwapCommand.NotifyCanExecuteChanged();
        GradientCommand.NotifyCanExecuteChanged();
    }

    private string DescribeColorSource(int index)
    {
        if (index < 0 || index >= _palette.Entries)
            return "";

        return _palette.ColorSources[index] switch
        {
            FileColorSource { Offset: var offset } when offset.BitOffset == 0 => $"File offset 0x{offset.ByteOffset:X}",
            FileColorSource { Offset: var offset } => $"File offset 0x{offset.ByteOffset:X} bit {offset.BitOffset}",
            ProjectNativeColorSource => "Native color stored in the project",
            ProjectForeignColorSource => $"{_palette.ColorModel} color stored in the project",
            _ => ""
        };
    }

    private void UpdateSourceHighlights()
    {
        int start = 0;
        foreach (var model in ColorSourceModels)
        {
            int count = model is FileColorSourceModel file ? file.Entries : 1;
            model.IsHighlighted = _selection.Indices.Any(i => i >= start && i < start + count);
            start += count;
        }
    }

    private EditableColorBaseViewModel CreateActiveColorEditor(IColor foreignColor, int index)
    {
        EditableColorBaseViewModel editor = foreignColor switch
        {
            IColor32 color32 => new Color32ViewModel(color32, index, _colorFactory, _palette.ColorModel),
            ITableColor tableColor => new TableColorViewModel(tableColor, index, _colorFactory, _palette.ColorModel),
            _ => throw new NotSupportedException($"Color of type '{foreignColor.GetType()}' is not supported for editing")
        };

        editor.SaveColorCommand = AssignActiveColorCommand;
        editor.IsReadOnly = IsReadOnly;
        return editor;
    }

    [RelayCommand]
    public void AddNewFileColorSource()
    {
        var size = _colorFactory.CreateColor(_palette.ColorModel).Size;
        var lastFile = ColorSourceModels.OfType<FileColorSourceModel>().LastOrDefault();
        var address = lastFile is null ? 0 : lastFile.FileAddress + (lastFile.Entries * size + 7) / 8;

        ColorSourceModels.Add(new FileColorSourceModel(address, 1, lastFile?.Endian ?? Endian.Little));
    }

    [RelayCommand]
    public void AddNewNativeColorSource()
    {
        var color = _colorFactory.CreateColor(ColorModel.Rgba32, 0, 0, 0, 255);
        var hexString = _colorFactory.ToHexString(color);
        ColorSourceModels.Add(new NativeColorSourceModel(hexString));
    }

    [RelayCommand]
    public void AddNewForeignColorSource()
    {
        var color = _colorFactory.CreateColor(_palette.ColorModel, 0);
        var hexString = _colorFactory.ToHexString(color);
        ColorSourceModels.Add(new ForeignColorSourceModel(hexString, _palette.ColorModel));
    }

    [RelayCommand]
    public void RemoveColorSource(ColorSourceModel model)
    {
        ColorSourceModels.Remove(model);
    }

    private void RebuildSourceModels()
    {
        ColorSourceModels.CollectionChanged -= OnSourceModelsCollectionChanged;
        foreach (var model in ColorSourceModels)
            model.PropertyChanged -= OnSourceModelPropertyChanged;

        ColorSourceModels = new(CreateColorSourceModels(_palette));
        _hasOutOfRangeSources = false;

        ColorSourceModels.CollectionChanged += OnSourceModelsCollectionChanged;
        foreach (var model in ColorSourceModels)
            model.PropertyChanged += OnSourceModelPropertyChanged;

        UpdateSourceHighlights();
    }

    private void OnSourceModelsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (var model in e.OldItems?.OfType<ColorSourceModel>() ?? [])
            model.PropertyChanged -= OnSourceModelPropertyChanged;
        foreach (var model in e.NewItems?.OfType<ColorSourceModel>() ?? [])
            model.PropertyChanged += OnSourceModelPropertyChanged;

        OnSourceModelsEdited();
    }

    private void OnSourceModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ColorSourceModel.IsHighlighted) or nameof(ColorSourceModel.HasErrors))
            return;

        OnSourceModelsEdited();
    }

    /// <summary>
    /// Applies valid source edits to the palette as an undoable step; invalid ones only mark the editor modified so Save is blocked
    /// </summary>
    private void OnSourceModelsEdited()
    {
        Entries = CountSourceColors();

        _hasOutOfRangeSources = false;
        if (!HasInvalidSources() && TryCreateColorSources(out var sources))
        {
            _isApplyingSourceEdit = true;
            try
            {
                _hasOutOfRangeSources = !_session.SetSources(sources);
                if (_hasOutOfRangeSources)
                    ActivityMessage = "A file source extends past the end of its data file";
            }
            finally
            {
                _isApplyingSourceEdit = false;
            }
        }

        UpdateSourceHighlights();
        UpdateModified();
    }

    private IEnumerable<ColorSourceModel> CreateColorSourceModels(Palette pal)
    {
        var size = _colorFactory.CreateColor(pal.ColorModel).Size;

        int i = 0;
        while (i < pal.ColorSources.Length)
        {
            if (pal.ColorSources[i] is FileColorSource fileSource)
            {
                int count = Palette.GetFileRunLength(pal.ColorSources, i, size);
                yield return new FileColorSourceModel(fileSource.Offset.ByteOffset, count, fileSource.Endian);
                i += count;
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
                var foreignSourceModel = new ForeignColorSourceModel(hexString, pal.ColorModel);
                yield return foreignSourceModel;
                i++;
            }
            else
            {
                throw new NotSupportedException($"Color source of type '{pal.ColorSources[i].GetType()}' is not supported");
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

    private bool TryCreateColorSources(out List<IColorSource> sources)
    {
        var colorModel = _palette.ColorModel;
        var size = _colorFactory.CreateColor(colorModel).Size;
        sources = [];

        foreach (var sourceModel in ColorSourceModels)
        {
            if (sourceModel is FileColorSourceModel fileModel)
            {
                var offset = new BitAddress(fileModel.FileAddress, 0);
                for (int j = 0; j < fileModel.Entries; j++)
                    sources.Add(new FileColorSource(offset + j * size, fileModel.Endian));
            }
            else if (sourceModel is NativeColorSourceModel nativeModel &&
                ColorParser.TryParse(nativeModel.NativeHexColor, ColorModel.Rgba32, out var nativeColor))
            {
                sources.Add(new ProjectNativeColorSource((ColorRgba32)nativeColor));
            }
            else if (sourceModel is ForeignColorSourceModel foreignModel &&
                ColorParser.TryParse(foreignModel.ForeignHexColor, colorModel, out var foreignColor))
            {
                sources.Add(new ProjectForeignColorSource(foreignColor));
            }
            else
            {
                return false;
            }
        }

        return true;
    }

    private bool TryParseColors(string text, out IReadOnlyList<IColor> colors)
    {
        var result = new List<IColor>();
        colors = result;

        foreach (var token in text.Split(['\r', '\n', ' ', '\t', ','], StringSplitOptions.RemoveEmptyEntries))
        {
            var hex = token.StartsWith('#') ? token : "#" + token;
            if (!ColorParser.TryParse(hex, ColorModel.Rgba32, out var native))
                return false;

            result.Add(_colorFactory.ToForeign((ColorRgba32)native, _palette.ColorModel));
        }

        return result.Count > 0;
    }

    private static string NormalizeLines(string? text) => text?.Replace("\r", "").Trim() ?? "";
}
