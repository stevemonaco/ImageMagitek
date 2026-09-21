using System.Windows.Input;
using Avalonia.Input;

namespace TileShop.UI.Models;

/// <summary>
/// A key gesture that runs a command from anywhere in the window while its owner is the active editor
/// </summary>
public sealed record Hotkey(KeyGesture Gesture, ICommand Command, object? Parameter = null)
{
    public Hotkey(string gesture, ICommand command, object? parameter = null)
        : this(KeyGesture.Parse(gesture), command, parameter)
    {
    }
}
