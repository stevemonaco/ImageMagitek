using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Schema;
using ImageMagitek.Project;
using Monaco.PathTree;

namespace ImageMagitek.Services
{
    public interface IProjectService
    {
        ISet<ProjectTree> Projects { get; }
        ISet<IProjectResource> DefaultResources { get; }

        MagitekResult LoadSchemaDefinition(string schemaFileName);
        void SetSchemaDefinition(XmlSchemaSet schemas);
        bool TryAddDefaultResource(IProjectResource resource);

        MagitekResult<ProjectTree> NewProject(string projectName);
        MagitekResults<ProjectTree> OpenProjectFile(string projectFileName);
        MagitekResult SaveProject(ProjectTree projectTree);
        MagitekResult SaveProjectAs(ProjectTree projectTree, string projectFileName);
        void CloseProject(ProjectTree projectTree);
        void CloseProjects();

        ProjectTree GetContainingProject(ResourceNode node);
        ProjectTree GetContainingProject(IProjectResource resource);
    }

    public class ProjectService : IProjectService
    {
        public ISet<ProjectTree> Projects { get => _projects; }
        public ISet<IProjectResource> DefaultResources { get => _resources; }

        private HashSet<ProjectTree> _projects = new HashSet<ProjectTree>();
        private HashSet<IProjectResource> _resources = new HashSet<IProjectResource>();

        private XmlSchemaSet _schemas = new XmlSchemaSet();
        private readonly ICodecService _codecService;

        public ProjectService(ICodecService codecService)
        {
            _codecService = codecService;
        }

        public ProjectService(ICodecService codecService, IEnumerable<IProjectResource> defaultResources)
        {
            _codecService = codecService;
            _resources = defaultResources.ToHashSet();
        }

        public bool TryAddDefaultResource(IProjectResource resource) => _resources.Add(resource);

        public MagitekResult<ProjectTree> NewProject(string projectFileName)
        {
            if (_projects.Any(x => string.Equals(x.Name, projectFileName, StringComparison.OrdinalIgnoreCase)))
                return new MagitekResult<ProjectTree>.Failed($"{projectFileName} already exists in the solution");

            var projectName = Path.GetFileNameWithoutExtension(projectFileName);
            var projectNode = new ProjectNode(projectName, new ImageProject(projectName));
            var projectTree = new ProjectTree(new PathTree<IProjectResource>(projectNode));
            projectTree.FileLocation = projectFileName;
            _projects.Add(projectTree);
            return new MagitekResult<ProjectTree>.Success(projectTree);
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

        public MagitekResults<ProjectTree> OpenProjectFile(string projectFileName)
        {
            if (string.IsNullOrWhiteSpace(projectFileName))
                throw new ArgumentException($"{nameof(OpenProjectFile)} cannot have a null or empty value for '{nameof(projectFileName)}'");

            if (!File.Exists(projectFileName))
                return new MagitekResults<ProjectTree>.Failed($"File '{projectFileName}' does not exist");

            try
            {
                var deserializer = new XmlGameDescriptorReader(_schemas, _codecService.CodecFactory);
                var result = deserializer.ReadProject(projectFileName);

                return result.Match(
                    success =>
                    {
                        Projects.Add(success.Result);
                        return result;
                    },
                    fail => result
                );
            }
            catch (Exception ex)
            {
                return new MagitekResults<ProjectTree>.Failed($"Failed to open project '{projectFileName}': {ex.Message}");
            }
        }

        public MagitekResult SaveProject(ProjectTree projectTree)
        {
            if (projectTree is null)
                throw new InvalidOperationException($"{nameof(SaveProject)} parameter '{nameof(projectTree)}' was null");

            if (string.IsNullOrWhiteSpace(projectTree.FileLocation))
                throw new InvalidOperationException($"{nameof(SaveProject)} cannot have a null or empty value for the project's file location");

            try
            {
                var serializer = new XmlGameDescriptorWriter();
                return serializer.WriteProject(projectTree, projectTree.FileLocation);
            }
            catch (Exception ex)
            {
                return new MagitekResult.Failed($"Failed to save project: {ex.Message}");
            }
        }

        public MagitekResult SaveProjectAs(ProjectTree projectTree, string projectFileName)
        {
            if (projectTree is null)
                throw new InvalidOperationException($"{nameof(SaveProjectAs)} parameter '{nameof(projectTree)}' was null");

            if (string.IsNullOrWhiteSpace(projectFileName))
                throw new ArgumentException($"{nameof(SaveProjectAs)} cannot have a null or empty value for '{nameof(projectFileName)}'");

            try
            {
                var serializer = new XmlGameDescriptorWriter();
                var result = serializer.WriteProject(projectTree, projectFileName);
                if (result.Value is MagitekResult.Success)
                    projectTree.FileLocation = projectFileName;

                return result;
            }
            catch (Exception ex)
            {
                return new MagitekResult.Failed($"Failed to save project: {ex.Message}");
            }
        }

        public void CloseProject(ProjectTree projectTree)
        {
            if (projectTree is null)
                throw new InvalidOperationException($"{nameof(CloseProject)} parameter '{nameof(projectTree)}' was null");

            if (_projects.Contains(projectTree))
            {
                foreach (var file in projectTree.Tree.EnumerateBreadthFirst().Select(x => x.Value).OfType<DataFile>())
                    file.Close();

                _projects.Remove(projectTree);
            }
        }

        public void CloseProjects()
        {
            foreach (var projectTree in _projects)
            {
                foreach (var file in projectTree.Tree.EnumerateDepthFirst().Select(x => x.Value).OfType<DataFile>())
                    file.Close();
            }
            _projects.Clear();
        }

        public ProjectTree GetContainingProject(ResourceNode node)
        {
            return _projects.FirstOrDefault(x => x.ContainsNode(node)) ??
                throw new ArgumentException($"{nameof(GetContainingProject)} could not locate the node '{node.PathKey}'");
        }

        public ProjectTree GetContainingProject(IProjectResource resource)
        {
            return _projects.FirstOrDefault(x => x.ContainsResource(resource)) ??
                throw new ArgumentException($"{nameof(GetContainingProject)} could not locate the resource '{resource.Name}'");
        }
    }
}
