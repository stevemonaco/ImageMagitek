using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using System.Linq;
using TileShop.AvaloniaUI.ViewModels;
using System;

namespace TileShop.AvaloniaUI.Views;
public partial class ProjectTreeView : UserControl
{
    private ProjectTreeViewModel? _viewModel;

    public ProjectTreeView()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        if (DataContext is ProjectTreeViewModel vm)
        {
            _viewModel = vm;
        }
        base.OnDataContextChanged(e);
    }

    private void ProjectTree_DoubleTapped(object? sender, RoutedEventArgs e)
    {
        var item = ((IVisual)e.Source!).GetSelfAndVisualAncestors()
            .OfType<TreeViewItem>()
            .FirstOrDefault();

        if (item is not null)
        {
            if (item.DataContext is FolderNodeViewModel || item.DataContext is ProjectNodeViewModel)
            {
                item.IsExpanded ^= true;
            }
            else 
            {
                _viewModel?.ActivateSelectedNode();
            }
            e.Handled = true;
        }
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

}
