using Avalonia.Input;
using CommunityToolkit.Mvvm.ComponentModel;

namespace TileShop.AvaloniaUI.Views;

internal interface IStateViewDriver<TViewModel> : IStateViewDriver
    where TViewModel : ObservableObject
{
    TViewModel? ViewModel { get; }
}

internal interface IStateViewDriver
{
    void OnKeyDown(object? sender, KeyEventArgs e);
    void OnKeyUp(object? sender, KeyEventArgs e);

    void OnPointerPressed(object sender, PointerPressedEventArgs e);
    void OnPointerReleased(object sender, PointerReleasedEventArgs e);
    void OnPointerMoved(object sender, PointerEventArgs e);
    void OnPointerExited(object sender, PointerEventArgs e);
    void OnPointerWheelChanged(object sender, PointerWheelEventArgs e);
}
