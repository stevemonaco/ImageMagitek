using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ImageMagitek;
using ImageMagitek.Codec;
using ImageMagitek.Project;
using Monaco.PathTree;
using TileShop.WPF.ViewModels;
using TileShop.Shared.Services;

namespace TileShop.WPF.Services
{
    public interface IProjectTreeService
    {
        public IPathTree<IProjectResource> Tree { get; }

        ImageProjectNodeViewModel NewProject(string projectName);
        ImageProjectNodeViewModel OpenProject(string projectFileName);
        bool SaveProject(string projectFileName);
        void UnloadProject();

        FolderNodeViewModel CreateNewFolder(TreeNodeViewModel parentNodeModel);

        IPathTreeNode<IProjectResource> AddResource(IProjectResource resource);
        IPathTreeNode<IProjectResource> AddResource(IProjectResource resource, IPathTreeNode<IProjectResource> parentNode);
        bool CanAddResource(IProjectResource resource);

        bool CanMoveNode(TreeNodeViewModel node, TreeNodeViewModel parentNode);
        void MoveNode(TreeNodeViewModel node, TreeNodeViewModel parentNode);
    }

    public class ProjectTreeService : IProjectTreeService
    {
        public IPathTree<IProjectResource> Tree { get; private set; }
        private CodecService _codecService;

        public ProjectTreeService(CodecService codecService)
        {
            _codecService = codecService;
        }

        public ImageProjectNodeViewModel NewProject(string projectName)
        {
            CloseResources();
            var project = new ImageProject(projectName);
            Tree = new PathTree<IProjectResource>(projectName, project);
            return new ImageProjectNodeViewModel(Tree.Root);
        }

        public ImageProjectNodeViewModel OpenProject(string projectFileName)
        {
            if (string.IsNullOrWhiteSpace(projectFileName))
                throw new ArgumentException($"{nameof(OpenProject)} cannot have a null or empty value for '{nameof(projectFileName)}'");

            CloseResources();
            var deserializer = new XmlGameDescriptorReader(_codecService.CodecFactory);
            Tree = deserializer.ReadProject(projectFileName, Path.GetDirectoryName(Path.GetFullPath(projectFileName)));
            return new ImageProjectNodeViewModel(Tree.Root);
        }

        public bool SaveProject(string projectFileName)
        {
            if (Tree is null)
                throw new InvalidOperationException($"{nameof(SaveProject)} does not have a tree");

            if (string.IsNullOrWhiteSpace(projectFileName))
                throw new ArgumentException($"{nameof(SaveProject)} cannot have a null or empty value for '{nameof(projectFileName)}'");

            var serializer = new XmlGameDescriptorWriter();
            return serializer.WriteProject(Tree, projectFileName);
        }

        public void UnloadProject()
        {
            CloseResources();
            Tree = null;
        }

        /// <summary>
        /// Creates a new folder with a default name under the given parentNodeModel
        /// </summary>
        /// <param name="parentNodeModel">Parent of the new folder</param>
        /// <returns>The new folder</returns>
        public FolderNodeViewModel CreateNewFolder(TreeNodeViewModel parentNodeModel)
        {
            var parentNode = parentNodeModel.Node;

            if (FindNewChildResourceName(parentNode, "New Folder") is string name)
            {
                var folder = new ResourceFolder(name);
                var folderNode = AddResource(folder, parentNode);
                var folderVm = new FolderNodeViewModel(folderNode)
                {
                    ParentModel = parentNodeModel
                };

                parentNodeModel.Children.Add(folderVm);
                return folderVm;
            }
            else
                return null;
        }

        public bool DeleteNode(TreeNodeViewModel nodeModel)
        {
            return false;
        }

        private string FindNewChildResourceName(IPathTreeNode<IProjectResource> node, string baseName)
        {
            return new string[] { baseName }
                .Concat(Enumerable.Range(1, 999).Select(x => $"{baseName} ({x})"))
                .FirstOrDefault(x => !node.ContainsChild(x));
        }

        public bool CanMoveNode(TreeNodeViewModel node, TreeNodeViewModel parentNode)
        {
            if (node is null || parentNode is null)
                return false;

            return CanMoveNode(node.Node, parentNode.Node);
        }

        private bool CanMoveNode(IPathTreeNode<IProjectResource> node, IPathTreeNode<IProjectResource> parentNode)
        {
            if (node is null || parentNode is null)
                return false;

            if (ReferenceEquals(node, parentNode))
                return false;

            if (node.Parent.PathKey == parentNode.PathKey)
                return false;

            if (parentNode.ContainsChild(node.Name))
                return false;

            if (!parentNode.Value.CanContainChildResources)
                return false;

            if (node is ResourceFolder && parentNode is ResourceFolder)
            {
                if (parentNode.Ancestors().Any(x => x.PathKey == node.PathKey))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Moves the specified node to the parentNode
        /// </summary>
        /// <param name="node">Node to be moved</param>
        /// <param name="parentNode">Parent to contain node after moving</param>
        public void MoveNode(TreeNodeViewModel node, TreeNodeViewModel parentNode)
        {
            MoveNode(node.Node, parentNode.Node);

            var oldParent = node.ParentModel;
            oldParent.Children.Remove(node);
            node.ParentModel = parentNode;
            parentNode.Children.Add(node);
        }

        private void MoveNode(IPathTreeNode<IProjectResource> node, IPathTreeNode<IProjectResource> parentNode)
        {
            node.Parent.DetachChild(node.Name);
            parentNode.AttachChild(node);
        }

        public bool CanAddResource(IProjectResource resource)
        {
            if (resource is null || Tree is null)
                return false;

            if (Tree.Root.Children().Any(x => string.Equals(x.Name, resource.Name, StringComparison.OrdinalIgnoreCase)))
                return false;

            return true;
        }

        public IPathTreeNode<IProjectResource> AddResource(IProjectResource resource)
        {
            var node = new PathTreeNode<IProjectResource>(resource.Name, resource);
            Tree.Root.AttachChild(node);
            return node;
        }

        public bool CanAddResource(IProjectResource resource, IPathTreeNode<IProjectResource> parentNode)
        {
            if (resource is null || parentNode is null)
                return false;

            if (parentNode.ContainsChild(resource.Name))
                return false;

            if (!parentNode.Value.CanContainChildResources)
                return false;

            return true;
        }

        public IPathTreeNode<IProjectResource> AddResource(IProjectResource resource, IPathTreeNode<IProjectResource> parentNode)
        {
            parentNode.AddChild(resource.Name, resource);
            parentNode.TryGetChild(resource.Name, out var addedNode);
            return addedNode;
        }

        private void CloseResources()
        {
            if (Tree is null)
                return;

            foreach (var file in Tree.EnumerateBreadthFirst().Select(x => x.Value).OfType<DataFile>())
                file.Close();
        }

        public IEnumerable<IPathTreeNode<IProjectResource>> Nodes() => Tree.EnumerateBreadthFirst();
    }
}
