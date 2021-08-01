using ImageMagitek;

namespace TileShop.WPF.Models
{
    public class RotateElementHistoryAction : HistoryAction
    {
        public override string Name => "Rotate Element";

        public RotationOperation Rotation { get; }
        public int ElementX { get; }
        public int ElementY { get; }

        public RotateElementHistoryAction(int elementX, int elementY, RotationOperation rotation)
        {
            ElementX = elementX;
            ElementY = elementY;
            Rotation = rotation;
        }
    }
}
