using System.Diagnostics.CodeAnalysis;
using System.Linq;
using ImageMagitek.Metatiles;
using ImageMagitek.Project;

namespace TileShop.UI.ViewModels;

public partial class MetatileMapNodeViewModel : ResourceNodeViewModel
{
    public string Description
    {
        get
        {
            var map = (MetatileMap)Node.Item;
            return $"{map.BlockFormat.Name} {map.Width}x{map.Height} block map at 0x{map.BlockOffset:X} of '{map.BlockSource.Name}', " +
                $"sets: {string.Join(", ", map.Sets.Select(x => x.Set.Name))}";
        }
    }

    [SetsRequiredMembers]
    public MetatileMapNodeViewModel(ResourceNode node, ResourceNodeViewModel? parent)
    {
        Node = node;
        Name = node.Name;
        ParentModel = parent;
    }

    internal override void RefreshDescription() => OnPropertyChanged(nameof(Description));
}