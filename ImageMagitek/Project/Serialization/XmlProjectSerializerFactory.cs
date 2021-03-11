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

        private readonly string _resourceSchemaFileName;
        private readonly ICodecFactory _codecFactory;
        private readonly IColorFactory _colorFactory;

        public XmlProjectSerializerFactory(string resourceSchemaFileName,
            ICodecFactory codecFactory, IColorFactory colorFactory, IEnumerable<IProjectResource> globalResources)
        {
            _resourceSchemaFileName = resourceSchemaFileName;
            _codecFactory = codecFactory;
            _colorFactory = colorFactory;
            GlobalResources = globalResources.ToList();
        }

        public IGameDescriptorReader CreateReader()
        {
            var resourceSchemas = CreateSchemas(_resourceSchemaFileName);

            return new XmlGameDescriptorMultiFileReader(resourceSchemas, _codecFactory, _colorFactory, GlobalResources);
        }

        public IGameDescriptorWriter CreateWriter()
        {
            return new XmlGameDescriptorMultiFileWriter(GlobalResources);
        }

        private XmlSchemaSet CreateSchemas(string resourceSchemaFileName)
        {
            var resourceSchemaText = File.ReadAllText(resourceSchemaFileName);

            var resourceSchema = XmlSchema.Read(new StringReader(resourceSchemaText),
                (sender, args) =>
                {
                });

            var resourceSchemaSet = new XmlSchemaSet();
            resourceSchemaSet.Add(resourceSchema);

            return resourceSchemaSet;
        }
    }
}
