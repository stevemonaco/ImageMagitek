using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using TileShop.AvaloniaUI.Windowing;

namespace TileShop.AvaloniaUI.ViewModels;
public sealed partial class ModifyGridSettingsViewModel : DialogViewModel<ModifyGridSettingsViewModel?>
{
    [ObservableProperty] private int _shiftX;
    [ObservableProperty] private int _shiftY;
    [ObservableProperty] private int _widthSpacing;
    [ObservableProperty] private int _heightSpacing;
    [ObservableProperty] private Color _primaryColor;
    [ObservableProperty] private Color _secondaryColor;
    [ObservableProperty] private Color _lineColor;

    protected override void Accept()
    {
        _requestResult = this;
        OnPropertyChanged(nameof(RequestResult));
    }
}
