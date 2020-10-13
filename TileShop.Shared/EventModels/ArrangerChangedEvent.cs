using ImageMagitek;

namespace TileShop.Shared.EventModels
{
    public enum ArrangerChange { Elements, Pixels, All }
    public class ArrangerChangedEvent
    {
        public Arranger Arranger { get; }
        public ArrangerChange Change { get; }

        public ArrangerChangedEvent(Arranger arranger, ArrangerChange change)
        {
            Arranger = arranger;
            Change = change;
        }
    }
}
