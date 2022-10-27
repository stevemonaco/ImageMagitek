using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using TileShop.AvaloniaUI.ViewModels;

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

    private void ProjectTree_DoubleTapped(object? sender, TappedEventArgs e)
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
