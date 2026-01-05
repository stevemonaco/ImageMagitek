using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using TileShop.UI.Controls;

namespace TileShop.UI.ViewModels;
public sealed partial class ModifyGridSettingsViewModel : RequestBaseViewModel<ModifyGridSettingsViewModel?>
{
    [ObservableProperty] private int _shiftX;
    [ObservableProperty] private int _shiftY;
    [ObservableProperty] private int _widthSpacing;
    [ObservableProperty] private int _heightSpacing;
    [ObservableProperty] private Color _primaryColor;
    [ObservableProperty] private Color _secondaryColor;
    [ObservableProperty] private Color _lineColor;

    public override ModifyGridSettingsViewModel? ProduceResult() => this;

    protected override void Accept()
    {
        RequestResult = this;
    }
}
