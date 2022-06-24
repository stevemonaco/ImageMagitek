using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Dock.Avalonia.Controls;
using TileShop.AvaloniaUI.ViewExtenders.Docking;
using TileShop.AvaloniaUI.ViewModels;

namespace TileShop.AvaloniaUI.Views;
public partial class ShellView : Window
{
    private ShellViewModel _viewModel = default!;
    private DockFactory _dockFactory = default!;

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

    protected override void OnDataContextChanged(EventArgs e)
    {
        if (DataContext is ShellViewModel viewModel)
        {
            _viewModel = viewModel;
        }
    }

    private void CreateDockingLayout()
    {
        _dockFactory = new DockFactory(_viewModel.ActiveTree, _viewModel.Editors);
        var layout = _dockFactory.CreateLayout();
        _dockFactory.InitLayout(layout);

        _dockFactory.FocusedDockableChanged += Factory_FocusedDockableChanged;

        var dock = this.FindControl<DockControl>("dock");
        dock.Layout = layout;
    }

    private void Factory_FocusedDockableChanged(object? sender, Dock.Model.Core.Events.FocusedDockableChangedEventArgs e)
    {
        if (e.Dockable is DockableEditorViewModel dock)
        {
            _viewModel.Editors.ActiveEditor = dock.Editor;
        }
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
