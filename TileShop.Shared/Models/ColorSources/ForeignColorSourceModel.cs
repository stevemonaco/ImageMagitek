using CommunityToolkit.Mvvm.ComponentModel;

namespace TileShop.Shared.Models;

public partial class ForeignColorSourceModel : ColorSourceModel
{
    [ObservableProperty] private string _foreignHexColor;

    public ForeignColorSourceModel(string foreignHexColor)
    {
        ForeignHexColor = foreignHexColor;
    }
}
