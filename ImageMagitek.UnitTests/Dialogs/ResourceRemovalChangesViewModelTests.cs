using ImageMagitek.Project;
using TileShop.Shared.Models;
using TileShop.UI.ViewModels;
using Xunit;

namespace ImageMagitek.UnitTests.Dialogs;

public sealed class ResourceRemovalChangesViewModelTests
{
    private static ResourceChangeViewModel Change(string name, bool removed, bool lostPalette, bool lostElement) =>
        new(new ResourceFolderNode(name, new ResourceFolder(name)), name, removed, lostPalette, lostElement);

    [Fact]
    public void KeptResources_ListedOnlyUnderChanged()
    {
        var removed = Change("removed", true, false, false);
        var lostElement = Change("lostElement", false, false, true);
        var lostPalette = Change("lostPalette", false, true, false);

        var vm = new ResourceRemovalChangesViewModel(removed, [removed, lostElement, lostPalette]);

        Assert.Equal([removed], vm.RemovedResources);
        Assert.Equal([lostElement, lostPalette], vm.ChangedResources);
        Assert.True(vm.HasRemovedResources);
        Assert.True(vm.HasChangedResources);
        Assert.Equal("loses elements", lostElement.ChangeDescription);
        Assert.Equal("uses default palette", lostPalette.ChangeDescription);
    }
}
