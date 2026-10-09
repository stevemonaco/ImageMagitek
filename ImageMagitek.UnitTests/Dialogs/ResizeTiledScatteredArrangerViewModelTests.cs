using TileShop.UI.ViewModels;
using Xunit;

namespace ImageMagitek.UnitTests.Dialogs;

public sealed class ResizeTiledScatteredArrangerViewModelTests
{
    private static ResizeTiledScatteredArrangerViewModel Create() => new(null!, 4, 3);

    [Theory]
    [InlineData(0, 3)]
    [InlineData(4, 0)]
    [InlineData(-1, 3)]
    [InlineData(4, -2)]
    public void CanAccept_ZeroOrNegative_IsFalse(int width, int height)
    {
        var vm = Create();

        vm.Width = width;
        vm.Height = height;

        Assert.False(vm.TryAcceptCommand.CanExecute(null));
    }

    [Fact]
    public void CanAccept_PositiveSize_IsTrue()
    {
        var vm = Create();

        vm.Width = 1;
        vm.Height = 7;

        Assert.True(vm.TryAcceptCommand.CanExecute(null));
    }
}
