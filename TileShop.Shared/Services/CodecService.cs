using System.Collections.Generic;
using System.IO;
using System.Linq;
using ImageMagitek.Codec;
using ImageMagitek.Colors;

namespace TileShop.Shared.Services
{
    public interface ICodecService
    {
        ICodecFactory CodecFactory { get; set; }

        IEnumerable<string> GetSupportedCodecNames();
        void LoadXmlCodecs(string codecsPath);
    }

    public class CodecService : ICodecService
    {
        private Palette _defaultPalette;
        public ICodecFactory CodecFactory { get; set; }

        public CodecService(Palette defaultPalette)
        {
            _defaultPalette = defaultPalette;
        }

        public void LoadXmlCodecs(string codecsPath)
        {
            var formats = new Dictionary<string, GraphicsFormat>();
            var serializer = new XmlGraphicsFormatReader();
            foreach (var formatFileName in Directory.GetFiles(codecsPath).Where(x => x.EndsWith(".xml")))
            {
                var format = serializer.LoadFromFile(formatFileName);
                formats.Add(format.Name, format);
            }

            CodecFactory = new CodecFactory(formats, _defaultPalette);
        }

        public IEnumerable<string> GetSupportedCodecNames() => CodecFactory.GetSupportedCodecNames();
    }
}
