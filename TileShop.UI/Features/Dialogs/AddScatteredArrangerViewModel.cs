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
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(TiledArrangerPixelWidth))] private int _tiledArrangerElementWidth;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(TiledArrangerPixelHeight))] private int _tiledArrangerElementHeight;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(TiledArrangerPixelWidth))] private int _tiledElementPixelWidth;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(TiledArrangerPixelHeight))] private int _tiledElementPixelHeight;
    public int TiledArrangerPixelWidth => TiledArrangerElementWidth * TiledElementPixelWidth;
    public int TiledArrangerPixelHeight => TiledArrangerElementHeight * TiledElementPixelHeight;

    [ObservableProperty] private int _singleArrangerPixelWidth;
    [ObservableProperty] private int _singleArrangerPixelHeight;
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

    public AddScatteredArrangerViewModel(IEnumerable<string> existingResourceNames, AddArrangerPreferences preferences)
    {
        _existingResourceNames = new(existingResourceNames);
        Title = "New Scattered Arranger";
        AcceptName = "Add";

        _selectedColorType = AvailableColorTypes.FirstOrDefault(x => x.Value == preferences.ColorType) ?? AvailableColorTypes.First();
        _selectedLayout = AvailableElementLayouts.FirstOrDefault(x => x.Value == preferences.Layout) ?? AvailableElementLayouts.First();
        _tiledArrangerElementWidth = preferences.TiledArrangerElementWidth;
        _tiledArrangerElementHeight = preferences.TiledArrangerElementHeight;
        _tiledElementPixelWidth = preferences.TiledElementPixelWidth;
        _tiledElementPixelHeight = preferences.TiledElementPixelHeight;
        _singleArrangerPixelWidth = preferences.SingleArrangerPixelWidth;
        _singleArrangerPixelHeight = preferences.SingleArrangerPixelHeight;
    }

    public override AddScatteredArrangerViewModel? ProduceResult() => this;

    public AddArrangerPreferences ToPreferences() => new(
        SelectedColorType.Value, SelectedLayout.Value,
        TiledArrangerElementWidth, TiledArrangerElementHeight,
        TiledElementPixelWidth, TiledElementPixelHeight,
        SingleArrangerPixelWidth, SingleArrangerPixelHeight);

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
