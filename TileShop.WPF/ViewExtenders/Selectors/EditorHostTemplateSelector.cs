using System.Windows;
using System.Windows.Controls;
using TileShop.WPF.ViewModels;

namespace TileShop.WPF.Selectors;

public class EditorHostTemplateSelector : DataTemplateSelector
{
    public DataTemplate PaletteEditorTemplate { get; set; }
    public DataTemplate ScatteredArrangerEditorTemplate { get; set; }
    public DataTemplate SequentialArrangerEditorTemplate { get; set; }
    public DataTemplate DataFileEditorTemplate { get; set; }
    public DataTemplate PixelEditorTemplate { get; set; }
    public DataTemplate ProjectTreeTemplate { get; set; }

    public override DataTemplate SelectTemplate(object item, DependencyObject container)
    {
        if (item == null || container is null)
            return null;

        return item switch
        {
            PaletteEditorViewModel _ => PaletteEditorTemplate,
            ScatteredArrangerEditorViewModel _ => ScatteredArrangerEditorTemplate,
            SequentialArrangerEditorViewModel _ => SequentialArrangerEditorTemplate,
            DataFileEditorViewModel _ => DataFileEditorTemplate,
            IndexedPixelEditorViewModel _ => PixelEditorTemplate,
            DirectPixelEditorViewModel _ => PixelEditorTemplate,
            ProjectTreeViewModel _ => ProjectTreeTemplate,
            _ => base.SelectTemplate(item, container)
        };
    }
}
