using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
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
        };
        
        Children.Add(dialog);
        mediator.Closed += MediatorOnClosed;
        dialog.Dismiss += DialogOnDismiss;
        
        return await tcs.Task;

        async void DialogOnDismiss(object? sender, RoutedEventArgs e)
        {
            var isCanceled = await mediator.TryCancel();

            if (isCanceled)
            {
                dialog.Dismiss -= DialogOnDismiss;
            }
        }
        
        void MediatorOnClosed(object? sender, EventArgs e)
        {
            mediator.Closed -= MediatorOnClosed;
            Children.Remove(dialog);
            tcs.SetResult(mediator.RequestResult);
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
    {
        var layer = new OverlayDialog
        {
            Content = content,
            Title = title,
            Options = options,
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
    /// Gets the number of currently open dialogs.
    /// </summary>
    public int DialogCount => _dialogStack.Count;

    /// <summary>
    /// Returns true if any dialog is currently open.
    /// </summary>
    public bool HasOpenDialog => _dialogStack.Count > 0;
}
