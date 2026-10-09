using ImageMagitek.Project;
using TileShop.Shared.Models;
using TileShop.UI.ViewModels;
using Xunit;

namespace ImageMagitek.UnitTests.Dialogs;

public sealed class AddScatteredArrangerViewModelTests
{
    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void InvalidName_DisablesAddUntilValid(string name)
    {
        var vm = new AddScatteredArrangerViewModel(n => ResourceName.Validate(n, false), new AddArrangerPreferences());

        vm.ArrangerName = name;

        Assert.False(vm.TryAcceptCommand.CanExecute(null));
        Assert.NotNull(vm.NameError);

        vm.ArrangerName = "Sprites";

        Assert.True(vm.TryAcceptCommand.CanExecute(null));
        Assert.Null(vm.NameError);
    }

    [Fact]
    public void CaseVariantOfSibling_DisablesAddAndShowsError()
    {
        var vm = new AddScatteredArrangerViewModel(SiblingValidator("Sprites"), new AddArrangerPreferences());

        vm.ArrangerName = "SPRITES";

        Assert.False(vm.TryAcceptCommand.CanExecute(null));
        Assert.Equal("'root' already contains 'Sprites'", vm.NameError);
    }

    private static System.Func<string, MagitekResult> SiblingValidator(string sibling) => n =>
        ResourceName.AreSame(n, sibling) ? new MagitekResult.Failed($"'root' already contains '{sibling}'") : ResourceName.Validate(n, false);
}