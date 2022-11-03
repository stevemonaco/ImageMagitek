using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using TileShop.AvaloniaUI.ViewModels;

namespace TileShop.AvaloniaUI.Views;
public partial class MenuView : UserControl
{
    private MenuViewModel _viewModel = default!;

    public MenuView()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        if (DataContext is MenuViewModel viewModel)
        {
            _viewModel = viewModel;
        }
    }

    private async void Exit_Click(object? sender, RoutedEventArgs e)
    {
        var canClose = await _viewModel.Shell.PrepareApplicationExit();

        if (!canClose)
            return;

        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown(0);
        }
    }
}
