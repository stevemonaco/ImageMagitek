using System.Windows.Controls;
using System.Windows.Media;

namespace TileShop.WPF.Views
{
    public partial class ScatteredArrangerEditorView : UserControl
    {
        public ScatteredArrangerEditorView()
        {
            InitializeComponent();

            var brush = TryFindResource("CheckeredBrush") as Brush;

            if (brush is not null)
                arrangerBackdrop.Fill = brush;
        }
    }
}
