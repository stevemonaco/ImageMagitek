using AvalonDock.Layout;
using System.Windows;
using System.Windows.Controls;
using TileShop.WPF.ViewModels;

namespace TileShop.WPF.Selectors;

public class DocumentHeaderTemplateSelector : DataTemplateSelector
{
    public DataTemplate PaletteDocumentHeaderTemplate { get; set; }
    public DataTemplate ScatteredArrangerDocumentHeaderTemplate { get; set; }
    public DataTemplate SequentialArrangerDocumentHeaderTemplate { get; set; }
    public DataTemplate DataFileDocumentHeaderTemplate { get; set; }
    public DataTemplate PixelDocumentHeaderTemplate { get; set; }
    public DataTemplate ProjectDocumentHeaderTemplate { get; set; }

    public override DataTemplate SelectTemplate(object item, DependencyObject container)
    {
        var document = item as LayoutDocument;
        if (document == null || container is null)
            return null;

        var content = document.Content;

        return content switch
        {
            PaletteEditorViewModel _ => PaletteDocumentHeaderTemplate,
            ScatteredArrangerEditorViewModel _ => ScatteredArrangerDocumentHeaderTemplate,
            SequentialArrangerEditorViewModel _ => SequentialArrangerDocumentHeaderTemplate,
            DataFileEditorViewModel _ => DataFileDocumentHeaderTemplate,
            IndexedPixelEditorViewModel _ => PixelDocumentHeaderTemplate,
            DirectPixelEditorViewModel _ => PixelDocumentHeaderTemplate,
            ProjectTreeViewModel _ => ProjectDocumentHeaderTemplate,
            _ => base.SelectTemplate(item, container)
        };
    }
}
