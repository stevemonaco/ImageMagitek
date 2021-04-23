using System;
using System.Linq;
using Monaco.PathTree;
using Monaco.PathTree.Abstractions;

namespace ImageMagitek.Project
{
    public sealed class ProjectTree : PathTreeBase<ResourceNode, IProjectResource>
    {
        public ImageProject Project => Root?.Item as ImageProject;
        public string Name => Root.Item.Name;

        public ProjectTree(ProjectNode root) :
            base(root)
        {
            if (root?.Item is not ImageProject)
                throw new ArgumentException($"{nameof(ProjectTree)} ctor called with invalid" +
                    $" root type '{root.GetType()}'");

            ExcludeRootFromPath = true;
        }

        /// <summary>
        /// Searches the entire tree to determine if the specified resource is contained within the tree
        /// </summary>
        /// <param name="resource">Resource to search</param>
        /// <returns></returns>
        public bool ContainsResource(IProjectResource resource)
        {
            return Root.SelfAndDescendantsDepthFirst<ResourceNode, IProjectResource>()
                .Any(x => ReferenceEquals(x.Item, resource));
        }

        /// <summary>
        /// Compares the node's root ancestor with the project root to determine if the tree contains the node
        /// </summary>
        /// <param name="node">Node to search</param>
        public bool ContainsNode(ResourceNode node) =>
            ReferenceEquals(node.SelfAndAncestors<ResourceNode, IProjectResource>().Last(), Root);

        /// <summary>
        /// Tries to find the resource node that contains the specified resource
        /// </summary>
        /// <param name="resource">Resource to locate</param>
        /// <param name="resourceNode">Result of search</param>
        /// <returns>True if found, false if not found</returns>
        public bool TryFindResourceNode(IProjectResource resource, out ResourceNode resourceNode)
        {
            resourceNode = Root.SelfAndDescendantsDepthFirst<ResourceNode, IProjectResource>()
                .FirstOrDefault(x => ReferenceEquals(x.Item, resource));

            return resourceNode is object;
        }

        /// <summary>
        /// Gets the resource node that contains the specified resource
        /// </summary>
        /// <param name="resource">Resource to locate</param>
        public ResourceNode GetResourceNode(IProjectResource resource) =>
            Root.SelfAndDescendantsDepthFirst<ResourceNode, IProjectResource>()
                .First(x => ReferenceEquals(x.Item, resource));

        /// <summary>
        /// Gets the resource node that contains the specified resource
        /// </summary>
        /// <param name="resource">Resource to locate</param>
        public T GetResourceNode<T>(IProjectResource resource) where T : ResourceNode =>
            (T) Root.SelfAndDescendantsDepthFirst<ResourceNode, IProjectResource>()
                .First(x => ReferenceEquals(x.Item, resource) && x is T);
    }
}
