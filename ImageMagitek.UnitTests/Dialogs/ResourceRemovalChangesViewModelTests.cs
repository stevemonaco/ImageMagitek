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

    [Fact]
    public void TilemapLosingTilePalette_ListedUnderChanged()
    {
        var removed = Change("removed", true, false, false);
        var node = new ResourceFolderNode("tilemap", new ResourceFolder("tilemap"));
        var tilemap = new ResourceChangeViewModel(new ResourceChange(node, "tilemap", false, false, false, LostTilePalette: true));

        var vm = new ResourceRemovalChangesViewModel(removed, [removed, tilemap]);

        Assert.Equal([tilemap], vm.ChangedResources);
        Assert.Equal("uses the generated tile palette", tilemap.ChangeDescription);
    }

    [Fact]
    public void ResourceLosingSlotPalette_ListedUnderChanged()
    {
        var removed = Change("removed", true, false, false);
        var node = new ResourceFolderNode("set", new ResourceFolder("set"));
        var set = new ResourceChangeViewModel(new ResourceChange(node, "set", false, false, false, LostSlotPalette: true));

        var vm = new ResourceRemovalChangesViewModel(removed, [removed, set]);

        Assert.Equal([set], vm.ChangedResources);
        Assert.Equal("removed palette slots use slot 0", set.ChangeDescription);
    }
}
