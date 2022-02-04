using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using CommunityToolkit.Mvvm.DependencyInjection;
using Dock.Avalonia.Controls;
using TileShop.AvaloniaUI.ViewExtenders.Docking;
using TileShop.AvaloniaUI.ViewModels;

namespace TileShop.AvaloniaUI.Views;
public partial class ShellView : Window
{
    public ShellView()
    {
        InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void CreateDockingLayout()
    {
        var vm = DataContext as ShellViewModel;
        var factory = new DockFactory(vm.ActiveTree, vm.Editors);
        var layout = factory.CreateLayout();
        factory.InitLayout(layout);

        var dock = this.FindControl<DockControl>("dock");
        dock.Layout = layout;
    }

    public void LoadLayout(object sender, RoutedEventArgs e)
    {
        CreateDockingLayout();
    }

    public void OnOpened(object sender, EventArgs e)
    {
        CreateDockingLayout();
    }
}
