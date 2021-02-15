using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImageMagitek.Project.Serialization
{
    public interface IProjectSerializerFactory
    {
        IGameDescriptorReader CreateReader();
        IGameDescriptorWriter CreateWriter();
    }
}
