using System.Windows;
using System.Windows.Controls;
using TileShop.WPF.ViewModels;

namespace TileShop.WPF.Selectors
{
    public class EditorHostStyleSelector : StyleSelector
    {
        public Style EditorStyle { get; set; }
        public Style ToolStyle { get; set; }

        public override Style SelectStyle(object item, DependencyObject container)
        {
            if (item is ResourceEditorBaseViewModel && !(item is PixelEditorViewModel))
                return EditorStyle;
            else if (item is ToolViewModel)
                return ToolStyle;
            else
                return base.SelectStyle(item, container);
        }
    }
}
