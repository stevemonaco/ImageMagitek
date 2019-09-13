using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ImageMagitek.Codec;
using ImageMagitek.Project;
using Monaco.PathTree;

namespace TileShop.Shared.Services
{
    public interface IProjectTreeService
    {
        IPathTree<IProjectResource> ReadProject(string projectFileName);
        bool SaveProject(IPathTree<IProjectResource> tree, string projectFileName);
    }

    public class ProjectTreeService : IProjectTreeService
    {
        private CodecService _codecService;

        public ProjectTreeService(CodecService codecService)
        {
            _codecService = codecService;
        }

        public IPathTree<IProjectResource> ReadProject(string projectFileName)
        {
            if (string.IsNullOrWhiteSpace(projectFileName))
                throw new ArgumentException($"{nameof(ReadProject)} cannot have a null or empty value for '{nameof(projectFileName)}'");

            IPathTree<IProjectResource> _tree = new PathTree<IProjectResource>();

            var deserializer = new XmlGameDescriptorReader(_codecService.CodecFactory);
            return deserializer.ReadProject(projectFileName, Path.GetDirectoryName(Path.GetFullPath(projectFileName)));
        }

        public bool SaveProject(IPathTree<IProjectResource> tree, string projectFileName)
        {
            if (tree is null)
                throw new ArgumentNullException($"{nameof(SaveProject)} cannot have a null value for '{nameof(tree)}'");

            if (string.IsNullOrWhiteSpace(projectFileName))
                throw new ArgumentException($"{nameof(SaveProject)} cannot have a null or empty value for '{nameof(projectFileName)}'");

            var serializer = new XmlGameDescriptorWriter();
            return serializer.WriteProject(tree, projectFileName);
        }
    }
}
