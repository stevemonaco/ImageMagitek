using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using TileShop.UI.ViewModels;

namespace TileShop.UI.Views;
public partial class ProjectTreeView : UserControl
{
    private ProjectTreeViewModel? _viewModel;

    public ProjectTreeView()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, ProjectTree_KeyDown, Avalonia.Interactivity.RoutingStrategies.Tunnel);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        if (DataContext is ProjectTreeViewModel vm)
        {
            _viewModel = vm;
        }
        base.OnDataContextChanged(e);
    }

    private async void ProjectTree_DoubleTapped(object? sender, TappedEventArgs e)
    {
        var item = ((Visual)e.Source!).GetSelfAndVisualAncestors()
            .OfType<TreeViewItem>()
            .FirstOrDefault();

        if (item is not null)
        {
            if (item.DataContext is FolderNodeViewModel or ProjectNodeViewModel)
            {
                item.IsExpanded ^= true;
            }
            else if (_viewModel is not null)
            {
                await _viewModel.ActivateSelectedNode();
            }
            e.Handled = true;
        }
    }

    private async void ProjectTree_KeyDown(object? sender, KeyEventArgs e)
    {
        if (_viewModel?.SelectedNode is not null && e.Key == Key.Enter)
        {
            e.Handled = true;

            if (_viewModel.SelectedNode is FolderNodeViewModel or ProjectNodeViewModel)
            {
                _viewModel.SelectedNode.IsExpanded ^= true;
            }
            else
            {
                await _viewModel.ActivateSelectedNode();
            }
        }
    }

    private async void ProjectNode_KeyDown(object? sender, KeyEventArgs e)
    {
        if (_viewModel?.SelectedNode is not null && e.Key == Key.Enter)
        {
            if (_viewModel.SelectedNode is FolderNodeViewModel or ProjectNodeViewModel)
            {
                _viewModel.SelectedNode.IsExpanded ^= true;
            }
            else
            {
                await _viewModel.ActivateSelectedNode();
            }
            e.Handled = true;
        }
    }
}
