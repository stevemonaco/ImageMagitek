using System.Collections.Generic;

namespace TileShop.WPF.Configuration
{
    public class AppSettings
    {
        public IDictionary<string, string> ExtensionCodecAssociations { get; set; }
        public IList<string> DefaultPalettes { get; set; }
        public string NesPalette { get; set; }
    }
}
