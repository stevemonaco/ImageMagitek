using System.Collections.ObjectModel;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using TileShop.Shared.Interactions;

namespace TileShop.UI.Controls;

/// <summary>
/// A container that hosts dialog layers, supporting multiple nested dialogs.
/// </summary>
public class DialogHost : Panel
{
    private readonly Stack<OverlayDialog> _dialogStack = new();

    public async Task<TResult?> ShowMediatorAsync<TResult>(IRequestMediator<TResult> mediator)
    {
        var tcs = new TaskCompletionSource<TResult?>();

        await mediator.OnOpening();
        
        var dialog = new OverlayDialog()
        {
            Content = mediator,
            Title = mediator.Title,
            Options = mediator.Options,
            // AcceptCommand = mediator.TryAcceptCommand,
            // CancelCommand = mediator.TryCancelCommand,
            // AcceptText = mediator.AcceptName,
            // CancelText = mediator.CancelName
        };
        
        Children.Add(dialog);
        mediator.Closed += MediatorOnClosed;
        return await tcs.Task;
        
        void MediatorOnClosed(object? sender, EventArgs e)
        {
            mediator.Closed -= MediatorOnClosed;
            
            Children.Remove(dialog);
            tcs.SetResult(mediator.Result);
        }
    }
    
    /// <summary>
    /// Shows a dialog with custom content.
    /// </summary>
    public async Task<bool> ShowDialogAsync(
        Control content,
        string title,
        ObservableCollection<RequestOption> options,
        bool showCancelButton = true)
        // string acceptText = "Ok",
        // string rejectText = "No",
        // string? cancelText = "Cancel",
        // IRelayCommand? acceptCommand = null,
        // IRelayCommand? rejectCommand = null,
        // IRelayCommand? cancelCommand = null)
    {
        var layer = new OverlayDialog
        {
            Content = content,
            Title = title,
            Options = options,
            // AcceptText = acceptText,
            // RejectText = rejectText,
            // CancelText = cancelText ?? "Cancel",
            // AcceptCommand = acceptCommand,
            // RejectCommand = rejectCommand,
            // CancelCommand = cancelCommand,
            ShowCancelButton = showCancelButton
        };

        Children.Add(layer);
        _dialogStack.Push(layer);

        try
        {
            var result = await layer.ShowAsync();
            return result;
        }
        finally
        {
            _dialogStack.Pop();
            Children.Remove(layer);
        }
    }

    /// <summary>
    /// Shows a dialog using an IRequestMediator.
    /// </summary>
    // public async Task<TResult?> ShowDialogAsync<TResult>(IRequestMediator<TResult> mediator, Control content)
    // {
    //     content.DataContext = mediator;
    //
    //     await ShowDialogAsync(
    //         content,
    //         mediator.Title,
    //         mediator.Options,
    //         mediator.AcceptName,
    //         acceptCommand: mediator.TryAcceptCommand,
    //         cancelText: mediator.CancelName,
    //         cancelCommand: mediator.TryCancelCommand);
    //
    //     return mediator.Result;
    // }

    /// <summary>
    /// Shows an alert dialog with a message.
    /// </summary>
    // public Task ShowAlertAsync(string title, string message, string acceptText = "Ok")
    // {
    //     var content = new SelectableTextBlock
    //     {
    //         Text = message,
    //         TextWrapping = Avalonia.Media.TextWrapping.Wrap
    //     };
    //
    //     return ShowDialogAsync(content, title, acceptText: acceptText);
    // }

    /// <summary>
    /// Shows a prompt dialog with choices.
    /// </summary>
    // public async Task<PromptResult> ShowPromptAsync(PromptChoice choices, string title, string? message = null)
    // {
    //     var content = message is not null
    //         ? new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap }
    //         : null;
    //
    //     var result = PromptResult.Cancel;
    //
    //     // For prompts with three options (Accept, Reject, Cancel), we need custom handling
    //     if (choices.Accept is not null && choices.Reject is not null && choices.Cancel is not null)
    //     {
    //         result = await ShowThreeButtonPromptAsync(choices, title, content);
    //     }
    //     else if (choices.Accept is not null && choices.Reject is not null)
    //     {
    //         // Yes/No style - map Reject to Cancel button behavior
    //         var dialogResult = await ShowDialogAsync(
    //             content as Control ?? new Panel(),
    //             title,
    //             choices.Accept,
    //             choices.Reject);
    //
    //         result = dialogResult ? PromptResult.Accept : PromptResult.Reject;
    //     }
    //     else if (choices.Accept is not null && choices.Cancel is not null)
    //     {
    //         var dialogResult = await ShowDialogAsync(
    //             content as Control ?? new Panel(),
    //             title,
    //             choices.Accept,
    //             choices.Cancel);
    //
    //         result = dialogResult ? PromptResult.Accept : PromptResult.Cancel;
    //     }
    //     else if (choices.Accept is not null)
    //     {
    //         await ShowDialogAsync(
    //             content as Control ?? new Panel(),
    //             title,
    //             choices.Accept,
    //             cancelText: null);
    //
    //         result = PromptResult.Accept;
    //     }
    //
    //     return result;
    // }
    //
    // private async Task<PromptResult> ShowThreeButtonPromptAsync(PromptChoice choices, string title, Control? messageContent)
    // {
    //     var tcs = new TaskCompletionSource<PromptResult>();
    //
    //     var layer = new OverlayDialog
    //     {
    //         Title = title,
    //         IsVisible = false,
    //         ShowCancelButton = false
    //     };
    //
    //     // Create custom content with three buttons
    //     var contentPanel = new StackPanel { Spacing = 16 };
    //
    //     if (messageContent is not null)
    //         contentPanel.Children.Add(messageContent);
    //
    //     var buttonPanel = new StackPanel
    //     {
    //         Orientation = Avalonia.Layout.Orientation.Horizontal,
    //         HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
    //         Spacing = 8
    //     };
    //
    //     var cancelButton = new Button { Content = choices.Cancel };
    //     var rejectButton = new Button { Content = choices.Reject };
    //     var acceptButton = new Button { Content = choices.Accept, Classes = { "Primary" } };
    //
    //     cancelButton.Click += (_, _) =>
    //     {
    //         tcs.TrySetResult(PromptResult.Cancel);
    //         _ = layer.CloseAsync(false);
    //     };
    //
    //     rejectButton.Click += (_, _) =>
    //     {
    //         tcs.TrySetResult(PromptResult.Reject);
    //         _ = layer.CloseAsync(false);
    //     };
    //
    //     acceptButton.Click += (_, _) =>
    //     {
    //         tcs.TrySetResult(PromptResult.Accept);
    //         _ = layer.CloseAsync(true);
    //     };
    //
    //     buttonPanel.Children.Add(cancelButton);
    //     buttonPanel.Children.Add(rejectButton);
    //     buttonPanel.Children.Add(acceptButton);
    //     contentPanel.Children.Add(buttonPanel);
    //
    //     layer.DialogContent = contentPanel;
    //
    //     Children.Add(layer);
    //     _dialogStack.Push(layer);
    //
    //     try
    //     {
    //         layer.IsVisible = true;
    //         await layer.ShowAsync();
    //         return await tcs.Task;
    //     }
    //     finally
    //     {
    //         _dialogStack.Pop();
    //         Children.Remove(layer);
    //     }
    // }

    /// <summary>
    /// Gets the number of currently open dialogs.
    /// </summary>
    public int DialogCount => _dialogStack.Count;

    /// <summary>
    /// Returns true if any dialog is currently open.
    /// </summary>
    public bool HasOpenDialog => _dialogStack.Count > 0;
}
