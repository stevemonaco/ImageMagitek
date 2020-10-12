using ImageMagitek;

namespace TileShop.WPF.EventModels
{
    public class AddScatteredArrangerFromCopyEvent
    {
        public ElementCopy Copy { get; set; }

        /// <summary>
        /// Event to create a new scattered arranger from a copy
        /// </summary>
        /// <param name="copy">Source containing all elements to be copied</param>
        public AddScatteredArrangerFromCopyEvent(ElementCopy copy)
        {
            Copy = copy;
        }
    }
}
