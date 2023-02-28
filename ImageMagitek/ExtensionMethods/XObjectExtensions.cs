using System.Xml.Linq;
using System.Xml;

namespace ImageMagitek.ExtensionMethods;
public static class XObjectExtensions
{
    public static int? LineNumber(this XObject element)
    {
        if (element is null)
            return default;

        var info = element as IXmlLineInfo;
        return info.HasLineInfo() ? info.LineNumber : default;
    }
}
