using System;
using System.Windows;
using System.Windows.Controls;
using TileShop.WPF.Models;

namespace TileShop.WPF.Selectors
{
    public class ColorSourceTemplateSelector : DataTemplateSelector
    {
        public DataTemplate FileSourceTemplate { get; set; }
        public DataTemplate NativeSourceTemplate { get; set; }
        public DataTemplate ForeignSourceTemplate { get; set; }
        public DataTemplate ScatteredSourceTemplate { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            if (item == null || container is null)
                return null;

            return item switch
            {
                FileColorSourceModel => FileSourceTemplate,
                NativeColorSourceModel => NativeSourceTemplate,
                ForeignColorSourceModel => ForeignSourceTemplate,
                ScatteredColorSourceModel => ScatteredSourceTemplate,
                _ => throw new ArgumentException($"{nameof(SelectTemplate)} does not contain a template for type {item.GetType()}")
            };
        }
    }
}
