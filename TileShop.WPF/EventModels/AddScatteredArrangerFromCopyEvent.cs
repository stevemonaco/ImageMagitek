using ImageMagitek;
using ImageMagitek.Project;

namespace TileShop.WPF.EventModels
{
    public class AddScatteredArrangerFromCopyEvent
    {
        /// <summary>
        /// Contains the copy to be made into a Scattered Arranger
        /// </summary>
        public ElementCopy Copy { get; set; }

        /// <summary>
        /// Resource that the copy originated from in project
        /// </summary>
        public IProjectResource ProjectResource { get; set; }

        /// <summary>
        /// Event to create a new scattered arranger from a copy
        /// </summary>
        /// <param name="copy">Source containing all elements to be copied</param>
        /// <param name="projectResource">Source resource within the the project</param>
        public AddScatteredArrangerFromCopyEvent(ElementCopy copy, IProjectResource projectResource)
        {
            Copy = copy;
            ProjectResource = projectResource;
        }
    }
}
