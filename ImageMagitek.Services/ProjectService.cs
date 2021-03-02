using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ImageMagitek.Project;
using ImageMagitek.Project.Serialization;
using Monaco.PathTree;

namespace ImageMagitek.Services
{
    public interface IProjectService
    {
        ISet<ProjectTree> Projects { get; }

        MagitekResult<ProjectTree> NewProject(string projectName);
        MagitekResults<ProjectTree> OpenProjectFile(string projectFileName);
        MagitekResult SaveProject(ProjectTree projectTree);
        MagitekResult SaveProjectAs(ProjectTree projectTree, string projectFileName);
        void CloseProject(ProjectTree projectTree);
        void CloseProjects();

        MagitekResult<ResourceNode> AddResource(ResourceNode parentNode, IProjectResource resource, bool saveProject);
        MagitekResult<ResourceNode> CreateNewFolder(ResourceNode parentNode, string name, bool saveProject);

        ProjectTree GetContainingProject(ResourceNode node);
        ProjectTree GetContainingProject(IProjectResource resource);
        bool AreResourcesInSameProject(IProjectResource a, IProjectResource b);
    }

    public class ProjectService : IProjectService
    {
        public ISet<ProjectTree> Projects { get; } = new HashSet<ProjectTree>();

        private readonly IProjectSerializerFactory _serializerFactory;

        public ProjectService(IProjectSerializerFactory serializerFactory)
        {
            _serializerFactory = serializerFactory;
        }

        public virtual MagitekResult<ProjectTree> NewProject(string projectFileName)
        {
            if (Projects.Any(x => string.Equals(x.Name, projectFileName, StringComparison.OrdinalIgnoreCase)))
                return new MagitekResult<ProjectTree>.Failed($"{projectFileName} already exists in the solution");

            var projectName = Path.GetFileNameWithoutExtension(projectFileName);
            var project = new ImageProject(projectName);
            var root = new ProjectNode(project.Name, project);
            var tree = new ProjectTree(root);
            tree.FileLocation = projectFileName;

            Projects.Add(tree);
            return new MagitekResult<ProjectTree>.Success(tree);
        }

        public virtual MagitekResults<ProjectTree> OpenProjectFile(string projectFileName)
        {
            if (string.IsNullOrWhiteSpace(projectFileName))
                throw new ArgumentException($"{nameof(OpenProjectFile)} cannot have a null or empty value for '{nameof(projectFileName)}'");

            if (!File.Exists(projectFileName))
                return new MagitekResults<ProjectTree>.Failed($"File '{projectFileName}' does not exist");

            try
            {
                var reader = _serializerFactory.CreateReader();
                var result = reader.ReadProject(projectFileName);

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
                return new MagitekResults<ProjectTree>.Failed($"Failed to open project '{projectFileName}' due to a {ex.GetType()}: {ex.Message}");
            }
        }

        public virtual MagitekResult SaveProject(ProjectTree projectTree)
        {
            if (projectTree is null)
                throw new InvalidOperationException($"{nameof(SaveProject)} parameter '{nameof(projectTree)}' was null");

            if (string.IsNullOrWhiteSpace(projectTree.FileLocation))
                throw new InvalidOperationException($"{nameof(SaveProject)} cannot have a null or empty value for the project's file location");

            try
            {
                var writer = _serializerFactory.CreateWriter();
                return writer.WriteProject(projectTree, projectTree.FileLocation);
            }
            catch (Exception ex)
            {
                return new MagitekResult.Failed($"Failed to save project: {ex.Message}");
            }
        }

        public virtual MagitekResult SaveProjectAs(ProjectTree projectTree, string projectFileName)
        {
            if (projectTree is null)
                throw new InvalidOperationException($"{nameof(SaveProjectAs)} parameter '{nameof(projectTree)}' was null");

            if (string.IsNullOrWhiteSpace(projectFileName))
                throw new ArgumentException($"{nameof(SaveProjectAs)} cannot have a null or empty value for '{nameof(projectFileName)}'");

            try
            {
                var serializer = _serializerFactory.CreateWriter();
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

        public virtual void CloseProject(ProjectTree projectTree)
        {
            if (projectTree is null)
                throw new InvalidOperationException($"{nameof(CloseProject)} parameter '{nameof(projectTree)}' was null");

            if (Projects.Contains(projectTree))
            {
                foreach (var file in projectTree.EnumerateBreadthFirst().Select(x => x.Item).OfType<DataFile>())
                    file.Close();

                Projects.Remove(projectTree);
            }
        }

        public virtual void CloseProjects()
        {
            var files = Projects.SelectMany(tree => tree.EnumerateDepthFirst().Select(x => x.Item).OfType<DataFile>());

            foreach (var file in files)
                file.Close();

            Projects.Clear();
        }

        public virtual MagitekResult<ResourceNode> AddResource(ResourceNode parentNode, IProjectResource resource, bool saveProject)
        {
            var projectTree = Projects.FirstOrDefault(x => x.ContainsNode(parentNode));

            if (projectTree is null)
                return new MagitekResult<ResourceNode>.Failed($"{parentNode.Item.Name} is not contained within any loaded project");


            var addResult = projectTree.AddResource(parentNode, resource);

            return addResult.Match(
                addSuccess =>
                {
                    if (saveProject)
                    {
                        var saveResult = SaveProject(projectTree);
                        return saveResult.Match(
                            saveSuccess => addResult,
                            saveFailed => new MagitekResult<ResourceNode>.Failed(saveFailed.Reason));
                    }
                    else
                        return addResult;

                },
                addFailed => addResult);
        }

        public virtual MagitekResult<ResourceNode> CreateNewFolder(ResourceNode parentNode, string name, bool saveProject)
        {
            var projectTree = Projects.FirstOrDefault(x => x.ContainsNode(parentNode));

            if (projectTree is null)
                return new MagitekResult<ResourceNode>.Failed($"{parentNode.Item.Name} is not contained within any loaded project");


            var addResult = projectTree.CreateNewFolder(parentNode, name);

            return addResult.Match(
                addSuccess =>
                {
                    if (saveProject)
                    {
                        var saveResult = SaveProject(projectTree);
                        return saveResult.Match(
                            saveSuccess => addResult,
                            saveFailed => new MagitekResult<ResourceNode>.Failed(saveFailed.Reason));
                    }
                    else
                        return addResult;

                },
                addFailed => addResult);
        }

        public virtual ProjectTree GetContainingProject(ResourceNode node)
        {
            return Projects.FirstOrDefault(x => x.ContainsNode(node)) ??
                throw new ArgumentException($"{nameof(GetContainingProject)} could not locate the node '{node.PathKey}'");
        }

        public virtual ProjectTree GetContainingProject(IProjectResource resource)
        {
            return Projects.FirstOrDefault(x => x.ContainsResource(resource)) ??
                throw new ArgumentException($"{nameof(GetContainingProject)} could not locate the resource '{resource.Name}'");
        }

        public virtual bool AreResourcesInSameProject(IProjectResource a, IProjectResource b)
        {
            var projectA = Projects.FirstOrDefault(x => x.ContainsResource(a));
            var projectB = Projects.FirstOrDefault(x => x.ContainsResource(b));

            return ReferenceEquals(projectA, projectB);
        }
    }
}
