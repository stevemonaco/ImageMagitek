using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageMagitek;
using TileShop.Shared.Interactions;
using TileShop.Shared.Models;

namespace TileShop.UI.ViewModels;
public partial class AddScatteredArrangerViewModel : RequestViewModel<AddScatteredArrangerViewModel>
{
    [ObservableProperty] private string _arrangerName = "";
    [ObservableProperty] private SelectionOption<PixelColorType> _selectedColorType;
    [ObservableProperty] private SelectionOption<ElementLayout> _selectedLayout;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(TiledArrangerPixelWidth))] private int _tiledArrangerElementWidth = 16;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(TiledArrangerPixelHeight))] private int _tiledArrangerElementHeight = 8;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(TiledArrangerPixelWidth))] private int _tiledElementPixelWidth = 8;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(TiledArrangerPixelHeight))] private int _tiledElementPixelHeight = 8;
    public int TiledArrangerPixelWidth => TiledArrangerElementWidth * TiledElementPixelWidth;
    public int TiledArrangerPixelHeight => TiledArrangerElementHeight * TiledElementPixelHeight;

    [ObservableProperty] private int _singleArrangerPixelWidth = 256;
    [ObservableProperty] private int _singleArrangerPixelHeight = 256;
    [ObservableProperty] private ObservableCollection<string> _existingResourceNames;
    [ObservableProperty] private ObservableCollection<string> _validationErrors = [];
    [ObservableProperty] private bool _canAdd;

    public List<SelectionOption<PixelColorType>> AvailableColorTypes { get; } = 
    [
        new(PixelColorType.Indexed, "Indexed", "All image pixels require a palette to display colors. The default palette will be used until a user-defined palette is applied."),
        new(PixelColorType.Direct, "Direct", "All image pixels contain full color information and require no palette to display colors")
    ];
    
    public List<SelectionOption<ElementLayout>> AvailableElementLayouts { get; } = 
    [
        new(ElementLayout.Tiled, "Tiled", "Allows many elements within the arranger, suitable for tile-based graphics"),
        new(ElementLayout.Single, "Single", "Restricts the arranger to a single element, suitable for pixel-based graphics")
    ];

    public AddScatteredArrangerViewModel(IEnumerable<string> existingResourceNames)
    {
        _existingResourceNames = new(existingResourceNames);
        Title = "New Scattered Arranger";
        AcceptName = "Add";
        
        SelectedColorType = AvailableColorTypes.First();
        SelectedLayout = AvailableElementLayouts.First();
    }

    public override AddScatteredArrangerViewModel? ProduceResult() => this;

    [RelayCommand]
    public void ValidateModel()
    {
        ValidationErrors.Clear();

        if (string.IsNullOrWhiteSpace(ArrangerName))
            ValidationErrors.Add($"Name is invalid");

        if (ExistingResourceNames.Contains(ArrangerName))
            ValidationErrors.Add($"Name already exists");

        CanAdd = ValidationErrors.Count == 0;
    }
}
