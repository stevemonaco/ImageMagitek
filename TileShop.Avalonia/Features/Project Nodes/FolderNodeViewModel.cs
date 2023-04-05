using ImageMagitek;
using ImageMagitek.Project;
using ImageMagitek.Colors;
using System.Diagnostics.CodeAnalysis;

namespace TileShop.UI.ViewModels;

public class FolderNodeViewModel : ResourceNodeViewModel
{
    public override int SortPriority => 1;

    [SetsRequiredMembers]
    public FolderNodeViewModel(ResourceNode node, ResourceNodeViewModel parent)
    {
        Node = node;
        Name = node.Name;

        foreach (var child in Node.ChildNodes)
        {
            ResourceNodeViewModel model;

            if (child.Item is ResourceFolder)
                model = new FolderNodeViewModel(child, this);
            else if (child.Item is Palette)
                model = new PaletteNodeViewModel(child, this);
            else if (child.Item is DataSource)
                model = new DataFileNodeViewModel(child, this);
            else if (child.Item is Arranger)
                model = new ArrangerNodeViewModel(child, this);
            else
                continue;

            Children.Add(model);
        }

        ParentModel = parent;
    }
}
