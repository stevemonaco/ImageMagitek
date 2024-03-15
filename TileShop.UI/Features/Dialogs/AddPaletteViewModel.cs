using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using ImageMagitek;
using ImageMagitek.Colors;
using TileShop.UI.Windowing;

namespace TileShop.UI.ViewModels;

public partial class AddPaletteViewModel : DialogViewModel<AddPaletteViewModel>
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
    [ObservableProperty] private bool _zeroIndexTransparent = true;
    [ObservableProperty] private ObservableCollection<string> _existingResourceNames;
    [ObservableProperty] private ObservableCollection<string> _validationErrors = new();
    [ObservableProperty] private bool _canAdd;

    public AddPaletteViewModel() : this(Enumerable.Empty<string>())
    {
    }

    public AddPaletteViewModel(IEnumerable<string> existingResourceNames)
    {
        _existingResourceNames = new(existingResourceNames);
        Title = "Add a New Palette";
        AcceptName = "Add";

        _selectedColorModel = ColorModels.First();
    }

    protected override void Accept()
    {
        _requestResult = this;
        OnPropertyChanged(nameof(RequestResult));
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
