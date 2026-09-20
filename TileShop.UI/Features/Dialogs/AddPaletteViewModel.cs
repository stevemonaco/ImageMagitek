using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using ImageMagitek;
using ImageMagitek.Colors;
using TileShop.Shared.Interactions;
using TileShop.Shared.Models;

namespace TileShop.UI.ViewModels;
public partial class AddPaletteViewModel : RequestViewModel<AddPaletteViewModel>
{
    private string _paletteName = "";
    public string PaletteName
    {
        get => _paletteName;
        set
        {
            if (SetProperty(ref _paletteName, value))
                ValidateModel();
        }
    }

    [ObservableProperty] private ObservableCollection<FileDataSource> _dataSources = new();
    [ObservableProperty] private FileDataSource? _selectedDataSource;
    [ObservableProperty] private ObservableCollection<string> _colorModels = new(Palette.GetColorModelNames());
    [ObservableProperty] private string _selectedColorModel;
    [ObservableProperty] private bool _zeroIndexTransparent;
    [ObservableProperty] private ObservableCollection<string> _existingResourceNames;
    [ObservableProperty] private ObservableCollection<string> _validationErrors = new();
    [ObservableProperty] private bool _canAdd;

    public AddPaletteViewModel() : this([], new())
    {
    }

    public AddPaletteViewModel(IEnumerable<string> existingResourceNames, AddPalettePreferences preferences)
    {
        _existingResourceNames = new(existingResourceNames);
        Title = "Add a New Palette";
        AcceptName = "Add";

        _selectedColorModel = ColorModels.Contains(preferences.ColorModel) ? preferences.ColorModel : ColorModels.First();
        _zeroIndexTransparent = preferences.ZeroIndexTransparent;
    }

    public override AddPaletteViewModel? ProduceResult() => this;

    public AddPalettePreferences ToPreferences() => new(SelectedColorModel, ZeroIndexTransparent);

    public void ValidateModel()
    {
        ValidationErrors.Clear();

        if (string.IsNullOrWhiteSpace(PaletteName))
            ValidationErrors.Add($"Name is invalid");

        if (ExistingResourceNames.Contains(PaletteName))
            ValidationErrors.Add($"Name already exists");

        CanAdd = ValidationErrors.Count == 0;
    }
}
