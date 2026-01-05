using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TileShop.UI.Controls;

namespace TileShop.UI.ViewModels;

public enum ElementLayoutFlowDirection { RowLeftToRight, ColumnTopToBottom }

public partial class CustomElementLayoutViewModel : RequestBaseViewModel<CustomElementLayoutViewModel>
{
    [ObservableProperty] private ElementLayoutFlowDirection _flowDirection;

    private int _width;
    public int Width
    {
        get => _width;
        set
        {
            if (SetProperty(ref _width, value))
                ValidateModel();
        }
    }

    private int _height;
    public int Height
    {
        get => _height;
        set
        {
            if (SetProperty(ref _height, value))
                ValidateModel();
        }
    }

    [ObservableProperty] private bool _canConfirm;
    [ObservableProperty] private ObservableCollection<string> _validationErrors = new();

    //protected override void OnInitialActivate()
    //{
    //    ValidateModel();
    //}

    public CustomElementLayoutViewModel()
    {
        Title = "Create Custom Tile Layout";
    }

    [RelayCommand]
    public void ValidateModel()
    {
        ValidationErrors.Clear();

        if (Width <= 0)
            ValidationErrors.Add($"{nameof(Width)} must be 1 or larger");

        if (Height <= 0)
            ValidationErrors.Add($"{nameof(Height)} must be 1 or larger");

        CanConfirm = ValidationErrors.Count == 0;
    }

    public override CustomElementLayoutViewModel? ProduceResult() => this;

    protected override void Accept()
    {
        RequestResult = this;
        OnPropertyChanged(nameof(RequestResult));
    }
}
