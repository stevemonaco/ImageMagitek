using ImageMagitek.Project;
using TileShop.UI.ViewModels;
using Xunit;

namespace ImageMagitek.UnitTests.Dialogs;

public sealed class NameResourceViewModelTests
{
    private static NameResourceViewModel Create(string initialName) =>
        new("Name", initialName, name => ResourceName.Validate(name, false));

    [Theory]
    [InlineData("")]
    [InlineData("a/b")]
    public void InvalidName_DisablesAcceptAndShowsError(string name)
    {
        var vm = Create("valid");

        vm.ResourceName = name;

        Assert.False(vm.TryAcceptCommand.CanExecute(null));
        Assert.Equal(ResourceName.Validate(name, false).AsError.Reason, vm.ErrorText);
    }

    [Fact]
    public void ValidName_EnablesAcceptAndClearsError()
    {
        var vm = Create("");

        vm.ResourceName = "Sprites";

        Assert.True(vm.TryAcceptCommand.CanExecute(null));
        Assert.Null(vm.ErrorText);
    }

    [Fact]
    public void InvalidInitialName_ShowsErrorImmediately()
    {
        var vm = Create("con");

        Assert.False(vm.TryAcceptCommand.CanExecute(null));
        Assert.NotNull(vm.ErrorText);
    }
}
