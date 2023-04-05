using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageMagitek;
using TileShop.UI.Windowing;

namespace TileShop.UI.ViewModels;

public partial class AddScatteredArrangerViewModel : DialogViewModel<AddScatteredArrangerViewModel>
{
    [ObservableProperty] private string _arrangerName = "";
    [ObservableProperty] private PixelColorType _colorType;
    [ObservableProperty] private ElementLayout _layout;
    [ObservableProperty] private int _arrangerElementWidth;
    [ObservableProperty] private int _arrangerElementHeight;
    [ObservableProperty] private int _elementPixelWidth;
    [ObservableProperty] private int _elementPixelHeight;
    [ObservableProperty] private ObservableCollection<string> _existingResourceNames;
    [ObservableProperty] private ObservableCollection<string> _validationErrors = new();
    [ObservableProperty] private bool _canAdd;

    public AddScatteredArrangerViewModel() : this(Enumerable.Empty<string>())
    {
    }

    public AddScatteredArrangerViewModel(IEnumerable<string> existingResourceNames)
    {
        _existingResourceNames = new(existingResourceNames);
        Title = "Add a New Scattered Arranger";
        AcceptName = "Add";
    }

    protected override void Accept()
    {
        if (Layout == ElementLayout.Single)
        {
            ArrangerElementHeight = 1;
            ArrangerElementWidth = 1;
        }

        _requestResult = this;
        OnPropertyChanged(nameof(RequestResult));
    }

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
