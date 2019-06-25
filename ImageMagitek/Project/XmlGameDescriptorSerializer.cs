using System;
using System.Collections.Generic;
using System.Text;

namespace ImageMagitek.Project
{
    public class XmlGameDescriptorSerializer : IGameDescriptorSerializer
    {
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
