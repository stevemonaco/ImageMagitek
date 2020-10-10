using System.Drawing;

namespace TileShop.WPF.Models
{
    public class PasteArrangerHistoryAction : HistoryAction
    {
        public override string Name => "Paste Arranger";

        public ArrangerPaste Paste { get; }
        public Point Location { get; set; }

        public PasteArrangerHistoryAction(ArrangerPaste paste, Point location)
        {
            Paste = paste;
            Location = location;
        }
    }
}
