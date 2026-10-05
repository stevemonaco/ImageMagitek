using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;

namespace TileShop.UI.Models;

/// <summary>
/// The commands an editor exposes to the Edit menu; use <see cref="Disabled"/> for an unsupported command
/// </summary>
public sealed record EditCommands(ICommand Undo, ICommand Redo, ICommand Cut, ICommand Copy, ICommand Paste, ICommand Delete, ICommand SelectAll)
{
    public static ICommand Disabled { get; } = new RelayCommand(() => { }, () => false);

    public static EditCommands None { get; } = new(Disabled, Disabled, Disabled, Disabled, Disabled, Disabled, Disabled);
}
