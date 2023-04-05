using System.Diagnostics.CodeAnalysis;
using ImageMagitek;
using ImageMagitek.Colors;
using ImageMagitek.Project;

namespace TileShop.UI.ViewModels;

public class ProjectNodeViewModel : ResourceNodeViewModel
{
    public override int SortPriority => 0;

    [SetsRequiredMembers]
    public ProjectNodeViewModel(ResourceNode node)
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
    }
}
