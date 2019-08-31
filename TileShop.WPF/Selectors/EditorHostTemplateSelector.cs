using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using TileShop.WPF.ViewModels;

namespace TileShop.WPF.Selectors
{
    public class EditorHostTemplateSelector : DataTemplateSelector
    {
        public DataTemplate PaletteEditorTemplate { get; set; }
        public DataTemplate ScatteredArrangerEditorTemplate { get; set; }
        public DataTemplate SequentialArrangerEditorTemplate { get; set; }
        public DataTemplate DataFileEditorTemplate { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            if (item == null || container is null)
                return null;

            var element = container as FrameworkElement;

            return item switch
            {
                PaletteEditorViewModel _ => PaletteEditorTemplate,
                ScatteredArrangerEditorViewModel _ => ScatteredArrangerEditorTemplate,
                SequentialArrangerEditorViewModel _ => SequentialArrangerEditorTemplate,
                DataFileEditorViewModel _ => DataFileEditorTemplate,
                _ => throw new ArgumentException($"{nameof(SelectTemplate)} does not contain a template for type {item.GetType()}")
            };
        }
    }
}
