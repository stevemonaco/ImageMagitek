using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Schema;
using ImageMagitek.Colors;
using ImageMagitek.Project;
using Monaco.PathTree;

namespace ImageMagitek.Services
{
    public interface ISolutionService
    {
        Dictionary<string, ProjectTree> ProjectTrees { get; }
        Dictionary<string, IProjectResource> DefaultResources { get; }

        MagitekResult LoadSchemaDefinition(string schemaFileName);
        void SetSchemaDefinition(XmlSchemaSet schemas);
        bool TryAddDefaultResource(IProjectResource resource);

        MagitekResult<ProjectTree> NewProject(string projectName);
        MagitekResults<ProjectTree> OpenProject(string projectFileName);
        MagitekResult SaveProject(ProjectTree projectTree, string projectFileName);
        void CloseProject(ProjectTree projectTree);

        MagitekResult<ResourceFolder> CreateNewFolder(IPathTreeNode<IProjectResource> parentNode, string name, bool useExactName);
        MagitekResult<IPathTreeNode<IProjectResource>> AddResource(IPathTreeNode<IProjectResource> parentNode, IProjectResource resource);

        MagitekResult CanMoveNode(IPathTreeNode<IProjectResource> node, IPathTreeNode<IProjectResource> parentNode);
        void MoveNode(IPathTreeNode<IProjectResource> node, IPathTreeNode<IProjectResource> parentNode);

        //ResourceRemovalChangesViewModel GetResourceRemovalChanges(TreeNodeViewModel rootNodeModel, TreeNodeViewModel removeNodeModel);
    }

    public class SolutionService : ISolutionService
    {
        public Dictionary<string, IProjectResource> DefaultResources { get; }
        public Dictionary<string, ProjectTree> ProjectTrees { get; } = new Dictionary<string, ProjectTree>();
        private XmlSchemaSet _schemas = new XmlSchemaSet();
        private readonly ICodecService _codecService;

        public SolutionService(ICodecService codecService, IEnumerable<IProjectResource> defaultResources)
        {
            _codecService = codecService;
            DefaultResources = defaultResources.ToDictionary(x => x.Name);
        }

        public bool TryAddDefaultResource(IProjectResource resource) =>
            DefaultResources.TryAdd(resource.Name, resource);

        public MagitekResult<ProjectTree> NewProject(string projectName)
        {
            var project = new PathTree<IProjectResource>(projectName, new ImageProject(projectName));
            var projectTree = new ProjectTree(project);
            if (ProjectTrees.TryAdd(projectName, projectTree))
                return new MagitekResult<ProjectTree>.Success(projectTree);
            else
                return new MagitekResult<ProjectTree>.Failed($"{projectName} already exists in the solution");
        }

        public MagitekResult LoadSchemaDefinition(string schemaFileName)
        {
            if (!File.Exists(schemaFileName))
                return new MagitekResult.Failed($"File '{schemaFileName}' does not exist");

            try
            {
                using var schemaStream = File.OpenRead(schemaFileName);
                _schemas = new XmlSchemaSet();
                _schemas.Add("", XmlReader.Create(schemaStream));
                return MagitekResult.SuccessResult;
            }
            catch (Exception ex)
            {
                return new MagitekResult.Failed($"{ex.Message}\n{ex.StackTrace}");
            }
        }

        public void SetSchemaDefinition(XmlSchemaSet schemas)
        {
            _schemas = schemas;
        }

        public MagitekResults<ProjectTree> OpenProject(string projectFileName)
        {
            if (string.IsNullOrWhiteSpace(projectFileName))
                throw new ArgumentException($"{nameof(OpenProject)} cannot have a null or empty value for '{nameof(projectFileName)}'");

            if (!File.Exists(projectFileName))
                return new MagitekResults<ProjectTree>.Failed($"File '{projectFileName}' does not exist");

            try
            {
                var deserializer = new XmlGameDescriptorReader(_schemas, _codecService.CodecFactory);
                return deserializer.ReadProject(projectFileName);
            }
            catch (Exception ex)
            {
                return new MagitekResults<ProjectTree>.Failed($"Failed to open project '{projectFileName}': {ex.Message}");
            }
        }

        public MagitekResult SaveProject(ProjectTree projectTree, string projectFileName)
        {
            if (projectTree is null)
                throw new InvalidOperationException($"{nameof(SaveProject)} parameter '{nameof(projectTree)}' was null");

            if (string.IsNullOrWhiteSpace(projectFileName))
                throw new ArgumentException($"{nameof(SaveProject)} cannot have a null or empty value for '{nameof(projectFileName)}'");

            try
            {
                var serializer = new XmlGameDescriptorWriter();
                return serializer.WriteProject(projectTree, projectFileName);
            }
            catch (Exception ex)
            {
                return new MagitekResult.Failed($"Failed to save project: {ex.Message}");
            }
        }

        public void CloseProject(ProjectTree projectTree)
        {
            if (ProjectTrees.ContainsKey(projectTree.Project.Name))
            {
                foreach (var file in projectTree.Tree.EnumerateBreadthFirst().Select(x => x.Value).OfType<DataFile>())
                    file.Close();

                ProjectTrees.Remove(projectTree.Project.Name);
            }
        }

        public MagitekResult<ResourceFolder> CreateNewFolder(IPathTreeNode<IProjectResource> parentNode, string name, bool useExactName)
        {
            if (!parentNode.ContainsChild(name) || !useExactName && parentNode.Value.CanContainChildResources)
            {
                var childName = FindFirstNewChildResourceName(parentNode, name);
                var folder = new ResourceFolder(childName);
                parentNode.AddChild(childName, folder);
                return new MagitekResult<ResourceFolder>.Success(folder);
            }
            else // if (parentNode.ContainsChild(name) && useExactName)
                return new MagitekResult<ResourceFolder>.Failed($"Could not create folder '{name}' under parent '{parentNode.Name}'");
        }

        public MagitekResult<IPathTreeNode<IProjectResource>> AddResource(IPathTreeNode<IProjectResource> parentNode, IProjectResource resource)
        {
            if (parentNode.ContainsChild(resource.Name))
            {
                return new MagitekResult<IPathTreeNode<IProjectResource>>.Failed($"'{parentNode.Name}' already contains a child named '{resource.Name}'");
            }
            else if (parentNode.Value.CanContainChildResources == false)
            {
                return new MagitekResult<IPathTreeNode<IProjectResource>>.Failed($"'{parentNode.Name}' cannot contain children");
            }
            else
            {
                if (resource is DataFile || resource is ScatteredArranger || resource is Palette || resource is ResourceFolder)
                {
                    parentNode.AddChild(resource.Name, resource);
                    parentNode.TryGetChild(resource.Name, out var childNode);
                    return new MagitekResult<IPathTreeNode<IProjectResource>>.Success(childNode);
                }
                else
                {
                    return new MagitekResult<IPathTreeNode<IProjectResource>>.Failed($"Cannot add a resource of type '{resource.GetType()}'"); ;
                }
            }
        }

        public MagitekResult CanMoveNode(IPathTreeNode<IProjectResource> node, IPathTreeNode<IProjectResource> parentNode)
        {
            if (node is null)
                throw new ArgumentNullException($"{nameof(CanMoveNode)} parameter '{node}' was null");

            if (parentNode is null)
                throw new ArgumentNullException($"{nameof(CanMoveNode)} parameter '{parentNode}' was null");

            if (ReferenceEquals(node, parentNode))
                return new MagitekResult.Failed($"Cannot move {node.Name} onto itself");

            if (node.Parent.PathKey == parentNode.PathKey)
                return new MagitekResult.Failed($"Cannot move {node.Name} onto itself");

            if (parentNode.ContainsChild(node.Name))
                return new MagitekResult.Failed($"{parentNode.Name} already contains {node.Name}");

            if (!parentNode.Value.CanContainChildResources)
                return new MagitekResult.Failed($"{parentNode.Name} cannot contain child resources");

            if (node is ResourceFolder && parentNode is ResourceFolder)
            {
                if (parentNode.Ancestors().Any(x => x.PathKey == node.PathKey))
                    return new MagitekResult.Failed($"{parentNode.Name} cannot contain child resources");
            }

            return MagitekResult.SuccessResult;
        }

        public void MoveNode(IPathTreeNode<IProjectResource> node, IPathTreeNode<IProjectResource> parentNode)
        {
            node.Parent.DetachChild(node.Name);
            parentNode.AttachChild(node);
        }

        private string FindFirstNewChildResourceName(IPathTreeNode<IProjectResource> node, string baseName)
        {
            if (node.ContainsChild(baseName))
                return baseName;
            else
                return new string[] { baseName }
                .Concat(Enumerable.Range(1, 999).Select(x => $"{baseName} ({x})"))
                .FirstOrDefault(x => !node.ContainsChild(x));
        }
    }
}
