using System.Threading.Tasks;
using Avalonia.Input;
using CommunityToolkit.Mvvm.Input;
using TileShop.Shared.Interactions;
using TileShop.UI.Controls;
using Xunit;

namespace ImageMagitek.UnitTests.Dialogs;

public class DialogKeyActionTests
{
    private static RequestOption Option(bool canExecute, bool isDefault = false, bool isCancel = false) =>
        new("Option", new AsyncRelayCommand(() => Task.CompletedTask, () => canExecute), isDefault, isCancel);

    [Fact]
    public void Escape_ExecutableCancel_RunsIt()
    {
        var cancel = Option(canExecute: true, isCancel: true);

        var (kind, option) = DialogKeyAction.Resolve(Key.Escape, [Option(true, isDefault: true), cancel]);

        Assert.Equal(DialogKeyActionKind.RunOption, kind);
        Assert.Same(cancel, option);
    }

    [Fact]
    public void Escape_NonExecutableCancel_DoesNothing()
    {
        var (kind, _) = DialogKeyAction.Resolve(Key.Escape, [Option(canExecute: false, isCancel: true)]);

        Assert.Equal(DialogKeyActionKind.None, kind);
    }

    [Fact]
    public void Escape_NoCancel_Dismisses()
    {
        var (kind, _) = DialogKeyAction.Resolve(Key.Escape, [Option(true, isDefault: true)]);

        Assert.Equal(DialogKeyActionKind.Dismiss, kind);
    }

    [Fact]
    public void Enter_NonExecutableDefault_DoesNothing()
    {
        var (kind, _) = DialogKeyAction.Resolve(Key.Enter, [Option(canExecute: false, isDefault: true)]);

        Assert.Equal(DialogKeyActionKind.None, kind);
    }
}
