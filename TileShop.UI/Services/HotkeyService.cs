using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using TileShop.UI.Models;

namespace TileShop.UI.Services;

/// <summary>
/// Dispatches the active editor's hotkeys from anywhere in the window so they work without the editor having keyboard focus
/// </summary>
public sealed class HotkeyService
{
    private const KeyModifiers _chordModifiers = KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Meta;

    private IReadOnlyList<Hotkey> _scope = [];
    private Func<bool>? _isSuspended;

    /// <param name="isSuspended">Returns true while hotkeys should be ignored, such as while a modal dialog is open</param>
    public void Attach(TopLevel root, Func<bool> isSuspended)
    {
        _isSuspended = isSuspended;
        root.AddHandler(InputElement.KeyDownEvent, OnKeyDown, RoutingStrategies.Bubble);
    }

    public void SetScope(IReadOnlyList<Hotkey> hotkeys) => _scope = hotkeys;

    public void ClearScope() => _scope = [];

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (_scope.Count == 0 || _isSuspended?.Invoke() == true)
            return;

        bool isTyping = e.Source is TextBox;

        foreach (var hotkey in _scope)
        {
            if (!hotkey.Gesture.Matches(e))
                continue;

            // Plain keys belong to the text box while typing; chords it didn't handle itself are fair game
            if (isTyping && (hotkey.Gesture.KeyModifiers & _chordModifiers) == 0)
                return;

            if (hotkey.Command.CanExecute(hotkey.Parameter))
            {
                hotkey.Command.Execute(hotkey.Parameter);
                e.Handled = true;
            }

            return;
        }
    }
}
