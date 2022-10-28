using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Dock.Model.Core.Events;
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

        _dock.Layout = layout;
    }

    private void Factory_FocusedDockableChanged(object? sender, FocusedDockableChangedEventArgs e)
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
