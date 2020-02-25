using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ImageMagitek;
using ImageMagitek.Codec;
using ImageMagitek.Project;
using Monaco.PathTree;
using TileShop.Shared.ViewModels;

namespace TileShop.Shared.Services
{
    public interface IProjectTreeService
    {
        public IPathTree<IProjectResource> Tree { get; }

        ImageProjectNodeViewModel NewProject(string projectName);
        ImageProjectNodeViewModel OpenProject(string projectFileName);
        bool SaveProject(string projectFileName);
        void UnloadProject();

        bool CanAddResource(IProjectResource resource, IPathTreeNode<IProjectResource> parentNode);
        IPathTreeNode<IProjectResource> AddResource(IProjectResource resource);
        IPathTreeNode<IProjectResource> AddResource(IProjectResource resource, IPathTreeNode<IProjectResource> parentNode);
        bool CanMoveNode(IPathTreeNode<IProjectResource> node, IPathTreeNode<IProjectResource> parentNode);
        void MoveNode(IPathTreeNode<IProjectResource> node, IPathTreeNode<IProjectResource> parentNode);
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

        public bool CanMoveNode(IPathTreeNode<IProjectResource> node, IPathTreeNode<IProjectResource> parentNode)
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

        public void MoveNode(IPathTreeNode<IProjectResource> node, IPathTreeNode<IProjectResource> parentNode)
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
