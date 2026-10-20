using System.Diagnostics.CodeAnalysis;
using ImageMagitek.Metatiles;
using ImageMagitek.Project;

namespace TileShop.UI.ViewModels;

public partial class MetatileSetNodeViewModel : ResourceNodeViewModel
{
    public string Description
    {
        get
        {
            var set = (MetatileSet)Node.Item;
            var tiles = set.TileSet;
            return $"{set.Count} {set.Shape.Width}x{set.Shape.Height} metatiles, {set.Layers} layers, " +
                $"{tiles.CodecName} tiles from {tiles.FirstTileIndex} at 0x{tiles.Offset:X} of '{tiles.Source.Name}'";
        }
    }

    [SetsRequiredMembers]
    public MetatileSetNodeViewModel(ResourceNode node, ResourceNodeViewModel? parent)
    {
        Node = node;
        Name = node.Name;
        ParentModel = parent;
    }

    internal override void RefreshDescription() => OnPropertyChanged(nameof(Description));
}