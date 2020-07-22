using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Schema;
using ImageMagitek.Project;
using Monaco.PathTree;

namespace ImageMagitek.Services
{
    public interface ISolutionService
    {
        Dictionary<string, ProjectTree> ProjectTrees { get; }
        Dictionary<string, IProjectResource> DefaultResources { get; }

        MagitekResult ApplySchemaDefinition(string schemaFileName);
        bool TryAddDefaultResource(IProjectResource resource);

        MagitekResult<ProjectTree> NewProject(string projectName);
        MagitekResults<ProjectTree> OpenProject(string projectFileName);
        MagitekResult SaveProject(ProjectTree projectTree, string projectFileName);
        void CloseProject(ProjectTree projectTree);

        //FolderNodeViewModel CreateNewFolder(TreeNodeViewModel parentNodeModel);
        //TreeNodeViewModel AddResource(TreeNodeViewModel parentModel, IProjectResource resource);

        //bool CanMoveNode(TreeNodeViewModel node, TreeNodeViewModel parentNode);
        //void MoveNode(TreeNodeViewModel node, TreeNodeViewModel parentNode);

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

        public MagitekResult ApplySchemaDefinition(string schemaFileName)
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
                return new MagitekResults<ProjectTree>.Failed($"Failed to open project: {ex.Message}");
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
    }
}
