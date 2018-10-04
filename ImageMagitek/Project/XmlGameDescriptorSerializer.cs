using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using System.Linq;
using ImageMagitek.Codec;

namespace ImageMagitek.Project
{
    public class XmlGameDescriptorSerializer : IGameDescriptorSerializer
    {
        public IDictionary<string, ProjectResourceBase> DeserializeProject(string fileName, string baseDirectory)
        {
            var stream = File.OpenRead(fileName);

            if (String.IsNullOrWhiteSpace(baseDirectory))
                throw new ArgumentException($"{nameof(DeserializeProject)} was called with a null or empty {nameof(baseDirectory)}");

            XElement doc = XElement.Load(stream);
            XElement projectNode = doc.Element("project");

            Directory.SetCurrentDirectory(baseDirectory);

            /*var settings = xe.Descendants("settings")
                .Select(e => new
                {
                    numberformat = e.Descendants("filelocationnumberformat").First().Value
                });*/

            var resourceTree = new Dictionary<string, ProjectResourceBase>();

            foreach (XElement node in projectNode.Elements())
            {
                if (node.Name == "folder")
                {
                    var folder = new ResourceFolder();
                    folder.Deserialize(node);
                    resourceTree.Add(node.Attribute("name").Value, folder);
                }
                else if (node.Name == "datafile")
                {
                    var df = new DataFile(node.Attribute("name").Value);
                    df.Deserialize(node);
                    resourceTree.Add(df.Name, df);
                }
                else if (node.Name == "palette")
                {
                    var pal = new Palette(node.Attribute("name").Value);
                    pal.Deserialize(node);
                    resourceTree.Add(pal.Name, pal);
                }
                else if (node.Name == "arranger")
                {
                    var arr = new ScatteredArranger();
                    arr.Rename(node.Attribute("name").Value);
                    arr.Deserialize(node);
                    resourceTree.Add(arr.Name, arr);
                }
            }

            return resourceTree;
        }

        public void SerializeProject(IDictionary<string, ProjectResourceBase> projectTree, string fileName)
        {
            throw new NotImplementedException();
        }

        /*
        public static void SerializeProject(Dictionary<string, ProjectResourceBase> projectTree, Stream stream)
        {
            if (projectTree is null)
                throw new ArgumentNullException($"SerializeProject was called with a null {nameof(projectTree)}");
            if (stream is null)
                throw new ArgumentNullException("SerializeProject was called with a null stream");
            if (!stream.CanWrite)
                throw new ArgumentException("SerializeProject was called with a stream without write access");

            var xmlRoot = new XElement("gdf");
            var projectRoot = new XElement("project");
            var settingsRoot = new XElement("settings");

            xmlRoot.Add(settingsRoot);
            xmlRoot.Add(projectRoot);

            var orderedNodes = projectTree.Values.OrderBy(x => x, new ProjectResourceBaseComparer()).Where(x => x.ShouldBeSerialized);
            orderedNodes.ForEach(x => projectRoot.Add(x.Serialize()));

            xmlRoot.Save(stream);
        } */
    }
}
