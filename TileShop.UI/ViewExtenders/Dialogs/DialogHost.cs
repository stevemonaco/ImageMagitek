using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using TileShop.Shared.Interactions;

namespace TileShop.UI.ViewExtenders.Dialogs;

/// <summary>
/// A container that hosts dialog layers, supporting multiple nested dialogs.
/// </summary>
public class DialogHost : TemplatedControl
{
    private readonly Stack<OverlayDialog> _dialogStack = new();
    private Panel? _layersPanel;
    private ContentPresenter? _contentPresenter;

    private static DialogHost? _current;

    /// <summary>
    /// Gets the currently active DialogHost instance.
    /// </summary>
    public static DialogHost? Current => _current;

    public static readonly StyledProperty<object?> HostContentProperty =
        AvaloniaProperty.Register<DialogHost, object?>(nameof(HostContent));

    public object? HostContent
    {
        get => GetValue(HostContentProperty);
        set => SetValue(HostContentProperty, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        _layersPanel = e.NameScope.Find<Panel>("PART_LayersPanel");
        _contentPresenter = e.NameScope.Find<ContentPresenter>("PART_ContentPresenter");
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _current = this;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_current == this)
            _current = null;
    }

    /// <summary>
    /// Shows a dialog with custom content.
    /// </summary>
    public async Task<bool> ShowDialogAsync(
        Control content,
        string title,
        string acceptText = "Ok",
        string? cancelText = "Cancel",
        IRelayCommand? acceptCommand = null,
        IRelayCommand? cancelCommand = null)
    {
        if (_layersPanel is null)
            throw new InvalidOperationException("DialogHost is not attached to the visual tree.");

        var layer = new OverlayDialog
        {
            DialogContent = content,
            Title = title,
            AcceptText = acceptText,
            CancelText = cancelText ?? "Cancel",
            ShowCancelButton = cancelText is not null
        };

        layer.BindCommands(acceptCommand, cancelCommand);

        _layersPanel.Children.Add(layer);
        _dialogStack.Push(layer);

        try
        {
            var result = await layer.ShowAsync();
            return result;
        }
        finally
        {
            layer.UnbindCommands();
            _dialogStack.Pop();
            _layersPanel.Children.Remove(layer);
        }
    }

    /// <summary>
    /// Shows a dialog using an IRequestMediator.
    /// </summary>
    public async Task<TResult?> ShowDialogAsync<TResult>(IRequestMediator<TResult> mediator, Control content)
    {
        content.DataContext = mediator;

        await ShowDialogAsync(
            content,
            mediator.Title,
            mediator.AcceptName,
            mediator.CancelName,
            mediator.AcceptCommand,
            mediator.CancelCommand);

        return mediator.RequestResult;
    }

    /// <summary>
    /// Shows an alert dialog with a message.
    /// </summary>
    public Task ShowAlertAsync(string title, string message)
    {
        var content = new TextBlock
        {
            Text = message,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap
        };

        return ShowDialogAsync(content, title, "Ok", cancelText: null);
    }

    /// <summary>
    /// Shows a prompt dialog with choices.
    /// </summary>
    public async Task<PromptResult> ShowPromptAsync(PromptChoice choices, string title, string? message = null)
    {
        if (_layersPanel is null)
            throw new InvalidOperationException("DialogHost is not attached to the visual tree.");

        var content = message is not null
            ? new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap }
            : null;

        var result = PromptResult.Cancel;

        // For prompts with three options (Accept, Reject, Cancel), we need custom handling
        if (choices.Accept is not null && choices.Reject is not null && choices.Cancel is not null)
        {
            result = await ShowThreeButtonPromptAsync(choices, title, content);
        }
        else if (choices.Accept is not null && choices.Reject is not null)
        {
            // Yes/No style - map Reject to Cancel button behavior
            var dialogResult = await ShowDialogAsync(
                content as Control ?? new Panel(),
                title,
                choices.Accept,
                choices.Reject);

            result = dialogResult ? PromptResult.Accept : PromptResult.Reject;
        }
        else if (choices.Accept is not null && choices.Cancel is not null)
        {
            var dialogResult = await ShowDialogAsync(
                content as Control ?? new Panel(),
                title,
                choices.Accept,
                choices.Cancel);

            result = dialogResult ? PromptResult.Accept : PromptResult.Cancel;
        }
        else if (choices.Accept is not null)
        {
            await ShowDialogAsync(
                content as Control ?? new Panel(),
                title,
                choices.Accept,
                cancelText: null);

            result = PromptResult.Accept;
        }

        return result;
    }

    private async Task<PromptResult> ShowThreeButtonPromptAsync(PromptChoice choices, string title, Control? messageContent)
    {
        if (_layersPanel is null)
            throw new InvalidOperationException("DialogHost is not attached to the visual tree.");

        var tcs = new TaskCompletionSource<PromptResult>();

        var layer = new OverlayDialog
        {
            Title = title,
            IsVisible = false,
            ShowCancelButton = false
        };

        // Create custom content with three buttons
        var contentPanel = new StackPanel { Spacing = 16 };

        if (messageContent is not null)
            contentPanel.Children.Add(messageContent);

        var buttonPanel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Spacing = 8
        };

        var cancelButton = new Button { Content = choices.Cancel };
        var rejectButton = new Button { Content = choices.Reject };
        var acceptButton = new Button { Content = choices.Accept, Classes = { "Primary" } };

        cancelButton.Click += (_, _) =>
        {
            tcs.TrySetResult(PromptResult.Cancel);
            _ = layer.CloseAsync(false);
        };

        rejectButton.Click += (_, _) =>
        {
            tcs.TrySetResult(PromptResult.Reject);
            _ = layer.CloseAsync(false);
        };

        acceptButton.Click += (_, _) =>
        {
            tcs.TrySetResult(PromptResult.Accept);
            _ = layer.CloseAsync(true);
        };

        buttonPanel.Children.Add(cancelButton);
        buttonPanel.Children.Add(rejectButton);
        buttonPanel.Children.Add(acceptButton);
        contentPanel.Children.Add(buttonPanel);

        layer.DialogContent = contentPanel;

        _layersPanel.Children.Add(layer);
        _dialogStack.Push(layer);

        try
        {
            layer.IsVisible = true;
            await layer.ShowAsync();
            return await tcs.Task;
        }
        finally
        {
            _dialogStack.Pop();
            _layersPanel.Children.Remove(layer);
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
