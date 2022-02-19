using CommunityToolkit.Mvvm.ComponentModel;

namespace TileShop.Shared.Models;

public partial class NativeColorSourceModel : ColorSourceModel
{
    [ObservableProperty] private string _nativeHexColor;

    public NativeColorSourceModel(string nativeHexColor)
    {
        NativeHexColor = nativeHexColor;
    }
}
