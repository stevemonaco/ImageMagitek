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

namespace TileShop.UI.ViewModels;

public partial class PaletteEditorViewModel : ResourceEditorBaseViewModel
{
    protected readonly Palette _palette;
    protected readonly IColorFactory _colorFactory;
    protected readonly IProjectService _projectService;

    [ObservableProperty] private ObservableCollection<EditableColorBaseViewModel> _colors = new();
    [ObservableProperty] private ObservableCollection<ColorSourceModel> _colorSourceModels = new();
    [ObservableProperty] private string _paletteSource;
    [ObservableProperty] private int _entries;
    [ObservableProperty] private ColorModel _colorModel;
    [ObservableProperty] private EditableColorBaseViewModel? _activeColor;

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
            if (SetProperty(ref _selectedColorIndex, value) && value >= 0 && value < Colors.Count)
            {
                var color = _palette.GetForeignColor(value);
                if (color is IColor32 color32)
                    ActiveColor = new Color32ViewModel(color32, value, _colorFactory);
                else if (color is ITableColor tableColor)
                    ActiveColor = new TableColorViewModel(tableColor, value, _colorFactory);
            }
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
        Colors = new(CreateColorModels());
        ColorSourceModels = new(CreateColorSourceModels(_palette));

        if (_palette.DataSource is not null)
            _paletteSource = _palette.DataSource.Name;
        else if (_palette.StorageSource == PaletteStorageSource.GlobalJson)
            _paletteSource = "[ReadOnly: JSON]";
        else
            _paletteSource = "[Unknown]";

        Entries = CountSourceColors();

        if (Entries > 0)
        {
            var color = _palette.GetForeignColor(0);
            if (color is IColor32 color32)
                ActiveColor = new Color32ViewModel(color32, 0, _colorFactory);
            else if (color is ITableColor tableColor)
                ActiveColor = new TableColorViewModel(tableColor, 0, _colorFactory);
        }
    }

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

        Colors = new(CreateColorModels());

        ActiveColor = Colors.First();
        Entries = CountSourceColors();
        SelectedColorIndex = 0;

        if (_palette.DataSource is not null)
            _paletteSource = _palette.DataSource.Name;
        else if (_palette.StorageSource == PaletteStorageSource.GlobalJson)
            _paletteSource = "[ReadOnly: JSON]";
        else
            _paletteSource = "[Unknown]";

        _palette.Reload();

        var changeMessage = new PaletteChangedMessage(_palette);
        Messenger.Send(changeMessage);

        IsModified = false;
    }

    [RelayCommand]
    public async Task SaveActiveColor()
    {
        if (ActiveColor is null)
            return;

        // The order here is very important as replacing a Colors item invalidates SelectedItem to -1 and
        // assigning a SelectedColorIndex reloads a color from the palette
        _palette.SetForeignColor(ActiveColor.Index, ActiveColor.WorkingColor);

        var model = CreateColorModel(_palette.GetForeignColor(SelectedColorIndex), SelectedColorIndex);
        var currentIndex = SelectedColorIndex;
        Colors[SelectedColorIndex] = model;

        SelectedColorIndex = currentIndex;
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

    public void MouseOver(Color32ViewModel model)
    {
        string contents = $"Palette Index: {model.Index}";
        var message = new NotifyStatusMessage(contents, NotifyStatusDuration.Indefinite);
        Messenger.Send(message);
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

    private IEnumerable<EditableColorBaseViewModel> CreateColorModels()
    {
        for (int i = 0; i < _palette.Entries; i++)
        {
            yield return CreateColorModel(_palette.GetForeignColor(i), i);
        }
    }

    private EditableColorBaseViewModel CreateColorModel(IColor foreignColor, int index)
    {
        if (foreignColor is IColor32 color32)
            return new Color32ViewModel(color32, index, _colorFactory);
        else if (foreignColor is ITableColor tableColor)
            return new TableColorViewModel(tableColor, index, _colorFactory);
        else
            throw new NotSupportedException($"Color of type '{foreignColor.GetType()}' is not supported for editing");
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
