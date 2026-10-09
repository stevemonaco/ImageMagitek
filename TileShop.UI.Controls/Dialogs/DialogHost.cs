using Avalonia.Controls;
using Avalonia.Interactivity;
using TileShop.Shared.Interactions;

namespace TileShop.UI.Controls;

/// <summary>
/// A container that hosts dialog layers, supporting multiple nested dialogs.
/// </summary>
public class DialogHost : Panel
{
    public async Task<TResult?> ShowMediatorAsync<TResult>(IRequestMediator<TResult> mediator)
    {
        var tcs = new TaskCompletionSource<TResult?>();

        await mediator.OnOpening();
        
        var dialog = new OverlayDialog()
        {
            Content = mediator,
            Title = mediator.Title,
            Options = mediator.Options,
            Size = mediator.Size,
        };
        
        Children.Add(dialog);
        mediator.Closed += MediatorOnClosed;
        dialog.Dismiss += DialogOnDismiss;
        
        return await tcs.Task;

        async void DialogOnDismiss(object? sender, RoutedEventArgs e)
        {
            await mediator.TryCancel();
        }

        // The overlay stays until its out-animation ends, so HasOpenDialog is false once the caller resumes
        async void MediatorOnClosed(object? sender, EventArgs e)
        {
            mediator.Closed -= MediatorOnClosed;
            dialog.Dismiss -= DialogOnDismiss;
            dialog.IsHitTestVisible = false;

            try
            {
                await dialog.AnimateOutAsync();
            }
            finally
            {
                Children.Remove(dialog);
                tcs.SetResult(mediator.RequestResult);
            }
        }
    }

    /// <summary>
    /// Gets the number of currently open dialogs.
    /// </summary>
    public int DialogCount => Children.Count;

    /// <summary>
    /// Returns true if any dialog is currently open.
    /// </summary>
    public bool HasOpenDialog => Children.Count > 0;
}
