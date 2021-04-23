using System.Collections.Generic;

namespace ImageMagitek.Project.Serialization
{
    public interface IProjectSerializerFactory
    {
        List<IProjectResource> GlobalResources { get; }

        IProjectReader CreateReader();
        IProjectWriter CreateWriter(ProjectTree tree);
    }
}
