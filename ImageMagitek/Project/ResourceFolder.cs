using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using MoreLinq;
using ImageMagitek;

namespace ImageMagitek.Project
{
    public class ResourceFolder: ProjectResourceBase
    {
        public ResourceFolder()
        {
            CanContainChildResources = true;
        }

        public override void Rename(string name)
        {
            Name = name;
        }

        public override ProjectResourceBase Clone()
        {
            ResourceFolder rf = new ResourceFolder();
            rf.Name = Name;

            return rf;
        }

        public override XElement Serialize()
        {
            XElement xeFolder = new XElement("folder");
            xeFolder.SetAttributeValue("name", Name);

            var orderedNodes = ChildResources.Values.OrderBy(x => x, new ProjectResourceBaseComparer()).Where(x => x.ShouldBeSerialized);
            orderedNodes.ForEach(x => xeFolder.Add(x.Serialize()));

            return xeFolder;
        }

        public override bool Deserialize(XElement element)
        {
            Name = element.Attribute("name").Value;

            foreach (XElement node in element.Elements())
            {
                if (node.Name == "folder")
                {
                    var folder = new ResourceFolder();
                    folder.Deserialize(node);
                    folder.Parent = this;
                    ChildResources.Add(folder.Name, folder);
                }
                else if (node.Name == "datafile")
                {
                    var df = new DataFile(node.Attribute("name").Value);
                    df.Deserialize(node);
                    df.Parent = this;
                    ChildResources.Add(df.Name, df);
                }
                else if (node.Name == "palette")
                {
                    var pal = new Palette(node.Attribute("name").Value);
                    pal.Deserialize(node);
                    pal.Parent = this;
                    ChildResources.Add(pal.Name, pal);
                }
                else if (node.Name == "arranger")
                {
                    var arr = new ScatteredArranger();
                    arr.Rename(node.Attribute("name").Value);
                    arr.Deserialize(node);
                    arr.Parent = this;
                    ChildResources.Add(arr.Name, arr);
                }
            }

            return true;
        }
    }
}
