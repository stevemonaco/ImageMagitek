using System.Threading.Tasks;
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
        var cd = new ContentDialog
        {
            PrimaryButtonText = "Ok",
            Title = title,
            Content = message,
            IsPrimaryButtonEnabled = true,
            IsSecondaryButtonEnabled = false,
            DefaultButton = ContentDialogButton.Primary,
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
    public async Task<TResult> RequestAsync<TResult>(IRequestMediator<TResult> mediator)
    {
        var content = _viewLocator.Build(mediator);
        content.DataContext = mediator;

        var cd = new ContentDialog()
        {
            Title = mediator.Title,
            Content = content,
            PrimaryButtonCommand = mediator.AcceptCommand,
            CloseButtonCommand = mediator.CancelCommand,
            PrimaryButtonText = mediator.AcceptName,
            CloseButtonText = mediator.CancelName,
        };

        await cd.ShowAsync();

        return mediator.RequestResult;
    }

    private ContentDialog ChoiceToDialog(PromptChoice choices)
    {
        return new()
        {
            PrimaryButtonText = choices.Accept,
            SecondaryButtonText = choices.Reject,
            CloseButtonText = choices.Cancel,
            //IsPrimaryButtonEnabled = choices.Accept is not null,
            //IsSecondaryButtonEnabled = choices.Reject is not null,
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
