using System.Collections.Generic;

namespace ImageMagitek.Project
{
    public abstract class ProjectResourceBase
    {
        /// <summary>
        /// Identifying name of the resource
        /// </summary>
        public string Name { get; protected set; }

        /// <summary>
        /// Determines if the ProjectResource can contain child resources
        /// </summary>
        /// <value>
        ///   <c>true</c> if this instance can contain child resources; otherwise, <c>false</c>.
        /// </value>
        public bool CanContainChildResources { get; protected set; }

        /// <summary>
        /// Gets a value indicating whether the ProjectResource should be serialized.
        /// </summary>
        /// <value>
        ///   <c>true</c> if [should be serialized]; otherwise, <c>false</c>.
        /// </value>
        public bool ShouldBeSerialized { get; set; } = true;

        /// <summary>
        /// Rename a resource with a new name
        /// </summary>
        /// <param name="name">The new name.</param>
        public virtual void Rename(string name) => Name = name;

        public abstract IEnumerable<ProjectResourceBase> LinkedResources();
    }
}
