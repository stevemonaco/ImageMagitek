using System;
using System.Windows;
using System.Windows.Controls;
using TileShop.WPF.ViewModels;

namespace TileShop.WPF.Selectors
{
    public class ProjectTreeTemplateSelector : DataTemplateSelector
    {
        public DataTemplate ProjectNodeTemplate { get; set; }
        public DataTemplate FolderNodeTemplate { get; set; }
        public DataTemplate ArrangerNodeTemplate { get; set; }
        public DataTemplate DataFileNodeTemplate { get; set; }
        public DataTemplate PaletteNodeTemplate { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            if (item == null || container is null)
                return null;

            return item switch
            {
                ProjectNodeViewModel _ => ProjectNodeTemplate,
                FolderNodeViewModel _ => FolderNodeTemplate,
                DataFileNodeViewModel _ => DataFileNodeTemplate,
                ArrangerNodeViewModel _ => ArrangerNodeTemplate,
                PaletteNodeViewModel _ => PaletteNodeTemplate,
                _ => throw new ArgumentException($"{nameof(SelectTemplate)} does not contain a template for type {item.GetType()}")
            };
        }
    }
}
