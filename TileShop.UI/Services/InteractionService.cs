using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.LogicalTree;
using TileShop.Shared.Interactions;
using TileShop.UI.Controls;
using TileShop.UI.Controls.Dialogs;

namespace TileShop.UI.Services;
internal class InteractionService : IInteractionService
{
    private readonly ViewLocator _viewLocator;

    public InteractionService(ViewLocator viewLocator)
    {
        _viewLocator = viewLocator;
    }

    /// <inheritdoc/>
    public async Task AlertAsync(string title, string message)
    {
        await Task.Yield(); // Yield ensures any ContextMenus are closed so focus isn't stolen

        var mediator = new AlertViewModel(title, message);
        var host = GetDialogHost();

        await host.ShowMediatorAsync(mediator);
    }

    /// <inheritdoc/>
    public async Task<PromptResult> PromptAsync(PromptChoice choices, string title, string message)
    {
        await Task.Yield(); // Yield ensures any ContextMenus are closed so focus isn't stolen

        var mediator = new PromptViewModel(choices, title, message);
        var host = GetDialogHost();
        
        return await host.ShowMediatorAsync(mediator);
        //var cd = ChoiceToDialog(choices);

        //cd.Title = title;
        //cd.Content = message;

        //var dialogResult = await cd.ShowAsync();

        //return DialogResultToPromptResult(dialogResult);
    }

    /// <inheritdoc/>
    public async Task<TResult?> RequestAsync<TResult>(IRequestMediator<TResult> mediator)
    {
        await Task.Yield(); // Yield ensures any ContextMenus are closed so focus isn't stolen
        var content = _viewLocator.Build(mediator);
        content.DataContext = mediator;
        
        var host = GetDialogHost();
        return await host.ShowMediatorAsync(mediator);

        //var dialog = new ContentDialog()
        //{
        //    Title = mediator.Title,
        //    Content = content,
        //    PrimaryButtonCommand = mediator.AcceptCommand,
        //    CloseButtonCommand = mediator.CancelCommand,
        //    PrimaryButtonText = mediator.AcceptName,
        //    CloseButtonText = mediator.CancelName,
        //    DefaultButton = ContentDialogButton.Primary
        //};

        //mediator.AcceptCommand.CanExecuteChanged += CanExecuteChanged;
        //await dialog.ShowAsync();
        //mediator.AcceptCommand.CanExecuteChanged -= CanExecuteChanged;

        //return mediator.RequestResult;

        //void CanExecuteChanged(object? sender, EventArgs e)
        //{
        //    dialog.IsPrimaryButtonEnabled = mediator.AcceptCommand.CanExecute(null);
        //}
    }

    //private ContentDialog ChoiceToDialog(PromptChoice choices)
    //{
    //    return new()
    //    {
    //        PrimaryButtonText = choices.Accept,
    //        SecondaryButtonText = choices.Reject,
    //        CloseButtonText = choices.Cancel,
    //        DefaultButton = ContentDialogButton.None
    //    };
    //}

    //private PromptResult DialogResultToPromptResult(ContentDialogResult result)
    //{
    //    return result switch
    //    {
    //        ContentDialogResult.None => PromptResult.Cancel,
    //        ContentDialogResult.Primary => PromptResult.Accept,
    //        ContentDialogResult.Secondary => PromptResult.Reject,
    //    };
    //}

    private DialogHost GetDialogHost(string hostId = "RootDialogHost")
    {
        DialogHost? host = null;
        var lifetime = (IClassicDesktopStyleApplicationLifetime)Application.Current!.ApplicationLifetime!;
        
        foreach (var window in lifetime.Windows)
        {
            host = window.GetLogicalDescendants().OfType<DialogHost>().FirstOrDefault(x => x.Name == hostId);
            if (host is not null)
                break;
        }
        
        return host ?? throw new InvalidOperationException($"Could not find a DialogHost with the name '{hostId}'");
    }
}
