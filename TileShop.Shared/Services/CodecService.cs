using ImageMagitek.Codec;
using ImageMagitek.Colors;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

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
        public ICodecFactory CodecFactory { get; set; }
        public Palette DefaultPalette { get; private set; }

        public CodecService(Palette defaultPalette)
        {
            DefaultPalette = defaultPalette;
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

            CodecFactory = new CodecFactory(formats, DefaultPalette);
        }

        public IEnumerable<string> GetSupportedCodecNames() => CodecFactory.GetSupportedCodecNames();
    }
}
