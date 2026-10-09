using ImageMagitek.Project;
using TileShop.Shared.Models;
using TileShop.UI.ViewModels;
using Xunit;

namespace ImageMagitek.UnitTests.Dialogs;

public sealed class AddPaletteViewModelTests
{
    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void InvalidName_DisablesAddUntilValid(string name)
    {
        var vm = new AddPaletteViewModel(n => ResourceName.Validate(n, false), new AddPalettePreferences());

        vm.PaletteName = name;

        Assert.False(vm.TryAcceptCommand.CanExecute(null));
        Assert.NotNull(vm.NameError);

        vm.PaletteName = "pal";

        Assert.True(vm.TryAcceptCommand.CanExecute(null));
        Assert.Null(vm.NameError);
    }

    [Fact]
    public void CaseVariantOfSibling_DisablesAddAndShowsError()
    {
        var vm = new AddPaletteViewModel(SiblingValidator("Pal"), new AddPalettePreferences());

        vm.PaletteName = "pal";

        Assert.False(vm.TryAcceptCommand.CanExecute(null));
        Assert.Equal("'root' already contains 'Pal'", vm.NameError);
    }

    private static System.Func<string, MagitekResult> SiblingValidator(string sibling) => n =>
        ResourceName.AreSame(n, sibling) ? new MagitekResult.Failed($"'root' already contains '{sibling}'") : ResourceName.Validate(n, false);
}