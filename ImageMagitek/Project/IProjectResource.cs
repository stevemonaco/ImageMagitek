using System.Collections.Generic;

namespace ImageMagitek.Project
{
    public interface IProjectResource
    {
        /// <summary>
        /// Identifying name of the resource
        /// </summary>
        string Name { get; set; }

        /// <summary>
        /// Determines if the ProjectResource can contain child resources
        /// </summary>
        /// <value>
        ///   <c>true</c> if this instance can contain child resources; otherwise, <c>false</c>.
        /// </value>
        bool CanContainChildResources { get; }

        /// <summary>
        /// Gets a value indicating whether the ProjectResource should be serialized.
        /// </summary>
        /// <value>
        ///   <c>true</c> if [should be serialized]; otherwise, <c>false</c>.
        /// </value>
        bool ShouldBeSerialized { get; set; }

        IEnumerable<IProjectResource> LinkedResources();
    }
}
