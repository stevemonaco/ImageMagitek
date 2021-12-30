using System;
using System.Windows;
using System.Windows.Controls;
using ImageMagitek;
using ImageMagitek.Colors;
using ImageMagitek.Project;
using TileShop.WPF.Models;

namespace TileShop.WPF.Selectors;

internal class ResourceRemovalTemplateSelector : DataTemplateSelector
{
    public DataTemplate FolderNodeTemplate { get; set; }
    public DataTemplate ArrangerNodeTemplate { get; set; }
    public DataTemplate DataFileNodeTemplate { get; set; }
    public DataTemplate PaletteNodeTemplate { get; set; }

    public override DataTemplate SelectTemplate(object item, DependencyObject container)
    {
        if (item == null || container is null)
            return null;

        if (item is ResourceChangeViewModel changeVm)
        {
            return changeVm.Resource switch
            {
                ResourceFolder _ => FolderNodeTemplate,
                DataSource _ => DataFileNodeTemplate,
                ScatteredArranger _ => ArrangerNodeTemplate,
                Palette _ => PaletteNodeTemplate,
                _ => throw new ArgumentException($"{nameof(SelectTemplate)} does not contain a template for type {changeVm.Resource.GetType()}")
            };
        }

        return base.SelectTemplate(item, container);
    }
}
