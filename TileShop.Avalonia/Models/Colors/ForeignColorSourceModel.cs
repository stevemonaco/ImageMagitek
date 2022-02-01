using CommunityToolkit.Mvvm.ComponentModel;

namespace TileShop.AvaloniaUI.Models;

public partial class ForeignColorSourceModel : ColorSourceModel
{
    [ObservableProperty] private string _foreignHexColor;

    public ForeignColorSourceModel(string foreignHexColor)
    {
        ForeignHexColor = foreignHexColor;
    }
}
