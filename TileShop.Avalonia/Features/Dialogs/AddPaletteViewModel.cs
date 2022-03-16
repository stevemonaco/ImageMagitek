using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageMagitek;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using TileShop.AvaloniaUI.ViewExtenders;

namespace TileShop.AvaloniaUI.ViewModels;

public partial class AddPaletteViewModel : DialogViewModel<AddPaletteViewModel>
{
    private string? _paletteName;
    public string? PaletteName
    {
        get => _paletteName;
        set
        {
            if (SetProperty(ref _paletteName, value))
                ValidateModel();
        }
    }
    [ObservableProperty] private ObservableCollection<DataSource> _dataSources = new();
    [ObservableProperty] private DataSource? _selectedDataSource;
    [ObservableProperty] private ObservableCollection<string> _colorModels = new();
    [ObservableProperty] private string? _selectedColorModel;
    [ObservableProperty] private bool _zeroIndexTransparent = true;
    [ObservableProperty] private ObservableCollection<string> _existingResourceNames;
    [ObservableProperty] private ObservableCollection<string> _validationErrors = new();
    [ObservableProperty] private bool _canAdd;

    public AddPaletteViewModel() : this(Enumerable.Empty<string>())
    {
    }

    public AddPaletteViewModel(IEnumerable<string> existingResourceNames)
    {
        ExistingResourceNames = new(existingResourceNames);
        Title = "Add a New Palette";
    }

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
