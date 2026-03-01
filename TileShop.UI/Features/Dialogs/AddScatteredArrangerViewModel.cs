using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageMagitek;
using TileShop.Shared.Interactions;

namespace TileShop.UI.ViewModels;
public partial class AddScatteredArrangerViewModel : RequestViewModel<AddScatteredArrangerViewModel>
{
    [ObservableProperty] private string _arrangerName = "";
    [ObservableProperty] private PixelColorType _selectedColorType = PixelColorType.Indexed;
    [ObservableProperty] private ElementLayout _selectedLayout = ElementLayout.Tiled;
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

    public List<PixelColorType> AvailableColorTypes { get; } = [PixelColorType.Indexed, PixelColorType.Direct];
    public List<ElementLayout> AvailableElementLayouts { get; } = [ElementLayout.Tiled, ElementLayout.Single];

    public AddScatteredArrangerViewModel(IEnumerable<string> existingResourceNames)
    {
        _existingResourceNames = new(existingResourceNames);
        Title = "Add a New Scattered Arranger";
        AcceptName = "Add";
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
