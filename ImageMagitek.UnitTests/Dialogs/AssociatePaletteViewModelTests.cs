using ImageMagitek.UnitTests.TestFactories;
using TileShop.Shared.Models;
using TileShop.UI.ViewModels;
using Xunit;

namespace ImageMagitek.UnitTests.Dialogs;

public sealed class AssociatePaletteViewModelTests
{
    [Fact]
    public void Empty_DisablesAccept()
    {
        var vm = new AssociatePaletteViewModel([]);

        Assert.Null(vm.SelectedPalette);
        Assert.False(vm.TryAcceptCommand.CanExecute(null));
    }

    [Fact]
    public void NonEmpty_SelectsFirstAndEnablesAccept()
    {
        var first = new AssociatePaletteModel(ArrangerTestFactory.CreatePalette(), "/first");
        var second = new AssociatePaletteModel(ArrangerTestFactory.CreatePalette(), "/second");

        var vm = new AssociatePaletteViewModel([first, second]);

        Assert.Same(first, vm.SelectedPalette);
        Assert.True(vm.TryAcceptCommand.CanExecute(null));
    }
}
