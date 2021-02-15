using ImageMagitek.Codec;
using ImageMagitek.Colors;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Schema;

namespace ImageMagitek.Project.Serialization
{
    public class XmlProjectSerializerFactory : IProjectSerializerFactory
    {
        public List<IProjectResource> GlobalResources { get; }

        private readonly string _projectSchemaFileName;
        private readonly string _resourceSchemaFileName;
        private readonly ICodecFactory _codecFactory;
        private readonly IColorFactory _colorFactory;

        public XmlProjectSerializerFactory(string projectSchemaFileName, string resourceSchemaFileName,
            ICodecFactory codecFactory, IColorFactory colorFactory, IEnumerable<IProjectResource> globalResources)
        {
            _projectSchemaFileName = projectSchemaFileName;
            _resourceSchemaFileName = resourceSchemaFileName;
            _codecFactory = codecFactory;
            _colorFactory = colorFactory;
            GlobalResources = globalResources.ToList();
        }

        public IGameDescriptorReader CreateReader()
        {
            var (projectSchemas, resourceSchemas) = CreateSchemas(_projectSchemaFileName, _resourceSchemaFileName);

            return new XmlGameDescriptorMultiFileReader(projectSchemas, resourceSchemas, _codecFactory, _colorFactory, GlobalResources);
        }

        public IGameDescriptorWriter CreateWriter()
        {
            return new XmlGameDescriptorMultiFileWriter();
        }

        private (XmlSchemaSet projectSchemas, XmlSchemaSet resourceSchemas) CreateSchemas(
            string projectSchemaFileName, string resourceSchemaFileName)
        {
            var projectSchemaText = File.ReadAllText(projectSchemaFileName);
            var resourceSchemaText = File.ReadAllText(resourceSchemaFileName);

            var projectSchema = XmlSchema.Read(new StringReader(projectSchemaText),
                (sender, args) =>
                {
                });

            var resourceSchema = XmlSchema.Read(new StringReader(resourceSchemaText),
                (sender, args) =>
                {
                });

            var projectSchemaSet = new XmlSchemaSet();
            projectSchemaSet.Add(projectSchema);

            var resourceSchemaSet = new XmlSchemaSet();
            resourceSchemaSet.Add(resourceSchema);

            return (projectSchemaSet, resourceSchemaSet);
        }
    }
}
