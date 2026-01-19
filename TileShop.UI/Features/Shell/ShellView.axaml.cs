using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using CommunityToolkit.Mvvm.DependencyInjection;
using Dock.Model.Core.Events;
using TileShop.Shared.Interactions;
using TileShop.UI.ViewExtenders.Docking;
using TileShop.UI.ViewModels;

namespace TileShop.UI.Views;
public partial class ShellView : Window
{
    private ShellViewModel _viewModel = default!;
    private DockFactory _dockFactory = default!;

    public ShellView()
    {
        InitializeComponent();
#if DEBUG
        debugLoadButton.IsVisible = true;
#endif
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

    private async void Button_OnClick(object? sender, RoutedEventArgs e)
    {
        var interactionService = Ioc.Default.GetRequiredService<IInteractionService>();
        await interactionService.AlertAsync("Title", "Message");
    }
    
    private async void PromptButton_OnClick(object? sender, RoutedEventArgs e)
    {
        var interactionService = Ioc.Default.GetRequiredService<IInteractionService>();
        var result = await interactionService.PromptAsync(PromptChoices.YesNoCancel, "Title", "Message");
        
        await interactionService.AlertAsync("Result", result.ToString());
    }
}
