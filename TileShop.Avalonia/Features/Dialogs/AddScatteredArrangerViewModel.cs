using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageMagitek;
using TileShop.AvaloniaUI.ViewExtenders;

namespace TileShop.AvaloniaUI.ViewModels;

public partial class AddScatteredArrangerViewModel : DialogViewModel<AddScatteredArrangerViewModel>
{
    [ObservableProperty] private string _arrangerName;
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
        ExistingResourceNames = new(existingResourceNames);
        Title = "Add a New Scattered Arranger";
    }

    public override void Ok(AddScatteredArrangerViewModel? result)
    {
        if (Layout == ElementLayout.Single)
        {
            ArrangerElementHeight = 1;
            ArrangerElementWidth = 1;
        }

        base.Ok(result);
    }

    [ICommand]
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
