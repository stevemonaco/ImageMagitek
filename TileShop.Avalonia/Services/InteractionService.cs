using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Layout;
using FluentAvalonia.UI.Controls;
using TileShop.Shared.Interactions;

namespace TileShop.AvaloniaUI.Services;
internal class InteractionService : IInteractionService
{
    private ViewLocator _viewLocator;

    public InteractionService(ViewLocator viewLocator)
    {
        _viewLocator = viewLocator;
    }

    /// <inheritdoc/>
    public async Task AlertAsync(string title, string message)
    {
        var titleBlock = new SelectableTextBlock()
        {
            Text = title,
            VerticalAlignment = VerticalAlignment.Top
        };

        var contentBlock = new SelectableTextBlock()
        {
            Text = message,
            VerticalAlignment = VerticalAlignment.Top
        };

        var cd = new ContentDialog
        {
            PrimaryButtonText = "Ok",
            Title = titleBlock,
            Content = contentBlock,
            IsPrimaryButtonEnabled = true,
            IsSecondaryButtonEnabled = false,
            DefaultButton = ContentDialogButton.Primary
        };

        await cd.ShowAsync();
    }

    /// <inheritdoc/>
    public async Task<PromptResult> PromptAsync(PromptChoice choices, string title, string? message = default)
    {
        var cd = ChoiceToDialog(choices);

        cd.Title = title;
        cd.Content = message;

        var dialogResult = await cd.ShowAsync();

        return DialogResultToPromptResult(dialogResult);
    }

    /// <inheritdoc/>
    public async Task<TResult?> RequestAsync<TResult>(IRequestMediator<TResult> mediator)
    {
        var content = _viewLocator.Build(mediator);
        content.DataContext = mediator;

        var dialog = new ContentDialog()
        {
            Title = mediator.Title,
            Content = content,
            PrimaryButtonCommand = mediator.AcceptCommand,
            CloseButtonCommand = mediator.CancelCommand,
            PrimaryButtonText = mediator.AcceptName,
            CloseButtonText = mediator.CancelName,
            DefaultButton = ContentDialogButton.Primary
        };

        mediator.AcceptCommand.CanExecuteChanged += CanExecuteChanged;
        await dialog.ShowAsync();
        mediator.AcceptCommand.CanExecuteChanged -= CanExecuteChanged;

        return mediator.RequestResult;

        void CanExecuteChanged(object? sender, EventArgs e)
        {
            dialog.IsPrimaryButtonEnabled = mediator.AcceptCommand.CanExecute(null);
        }
    }

    private ContentDialog ChoiceToDialog(PromptChoice choices)
    {
        return new()
        {
            PrimaryButtonText = choices.Accept,
            SecondaryButtonText = choices.Reject,
            CloseButtonText = choices.Cancel,
            DefaultButton = ContentDialogButton.None
        };
    }

    private PromptResult DialogResultToPromptResult(ContentDialogResult result)
    {
        return result switch
        {
            ContentDialogResult.None => PromptResult.Cancel,
            ContentDialogResult.Primary => PromptResult.Accept,
            ContentDialogResult.Secondary => PromptResult.Reject,
        };
    }
}
