using System.IO;
using System.Text;

namespace ImageMagitek.Project.Serialization
{
    internal sealed class Utf8StringWriter : StringWriter
    {
        public override Encoding Encoding => Encoding.UTF8;
    }
}
