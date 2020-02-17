using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using TileShop.WPF.ViewModels;

namespace TileShop.WPF.Selectors
{
    public class EditorHostStyleSelector : StyleSelector
    {
        public Style ArrangerEditorStyle { get; set; }
        public Style PaletteEditorStyle { get; set; }
        public Style DataFileEditorStyle { get; set; }
        public Style PixelEditorStyle { get; set; }
        public Style ProjectTreeStyle { get; set; }

        public override Style SelectStyle(object item, DependencyObject container) =>
            item switch
            {
                ScatteredArrangerEditorViewModel _ => ArrangerEditorStyle,
                SequentialArrangerEditorViewModel _ => ArrangerEditorStyle,
                PaletteEditorViewModel _ => PaletteEditorStyle,
                DataFileEditorViewModel _ => DataFileEditorStyle,
                PixelEditorViewModel _ => PixelEditorStyle,
                ProjectTreeViewModel _ => ProjectTreeStyle,
                _ => base.SelectStyle(item, container)
            };
    }
}
