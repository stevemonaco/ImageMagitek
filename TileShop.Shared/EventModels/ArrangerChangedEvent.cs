using ImageMagitek;

namespace TileShop.Shared.EventModels;

public enum ArrangerChange { Elements, Pixels, All }

public record ArrangerChangedEvent(Arranger Arranger, ArrangerChange Change);
