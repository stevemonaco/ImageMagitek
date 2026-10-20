using System.Diagnostics.CodeAnalysis;
using ImageMagitek;
using ImageMagitek.Project;

namespace TileShop.UI.ViewModels;

public partial class TilemapNodeViewModel : ResourceNodeViewModel
{
    public string Description
    {
        get
        {
            var tilemap = (TilemapArranger)Node.Item;
            var size = tilemap.ArrangerElementSize;
            return $"{tilemap.MapFormat.Name} {size.Width}x{size.Height} map at 0x{tilemap.MapOffset:X} of '{tilemap.MapSource.Name}', " +
                $"{tilemap.CodecName} tiles at 0x{tilemap.TileOffset:X} of '{tilemap.TileSource.Name}' (read-only)";
        }
    }

    [SetsRequiredMembers]
    public TilemapNodeViewModel(ResourceNode node, ResourceNodeViewModel? parent)
    {
        Node = node;
        Name = node.Name;
        ParentModel = parent;
    }

    internal override void RefreshDescription() => OnPropertyChanged(nameof(Description));
}
