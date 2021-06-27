namespace TileShop.WPF.Models
{
    public class FloodFillAction<TColor> : HistoryAction
        where TColor : struct
    {
        public override string Name => "Flood Fill";

        private TColor _fillColor;
        public TColor FillColor
        {
            get => _fillColor;
            set => SetAndNotify(ref _fillColor, value);
        }

        private int _x;
        public int X
        {
            get => _x;
            set => SetAndNotify(ref _x, value);
        }

        private int _y;
        public int Y
        {
            get => _y;
            set => SetAndNotify(ref _y, value);
        }

        public FloodFillAction(int x, int y, TColor fillColor)
        {
            X = x;
            Y = y;
            FillColor = fillColor;
        }
    }
}
