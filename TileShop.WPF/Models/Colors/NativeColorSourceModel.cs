namespace TileShop.WPF.Models;

public class NativeColorSourceModel : ColorSourceModel
{
    private string _nativeHexColor;
    public string NativeHexColor
    {
        get => _nativeHexColor;
        set => SetAndNotify(ref _nativeHexColor, value);
    }

    public NativeColorSourceModel(string nativeHexColor)
    {
        NativeHexColor = nativeHexColor;
    }
}
