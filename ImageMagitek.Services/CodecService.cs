using System.Collections.Generic;
using System.IO;
using System.Linq;
using ImageMagitek.Codec;
using ImageMagitek.Colors;

namespace ImageMagitek.Services
{
    public interface ICodecService
    {
        ICodecFactory CodecFactory { get; set; }

        IEnumerable<string> GetSupportedCodecNames();
        MagitekResults LoadXmlCodecs(string codecsPath);
    }

    public class CodecService : ICodecService
    {
        public ICodecFactory CodecFactory { get; set; }

        private readonly string _schemaFileName;
        private readonly Palette _defaultPalette;

        public CodecService(string schemaFileName, Palette defaultPalette)
        {
            _schemaFileName = schemaFileName;
            _defaultPalette = defaultPalette;
        }

        public MagitekResults LoadXmlCodecs(string codecsPath)
        {
            var formats = new Dictionary<string, GraphicsFormat>();
            var serializer = new XmlGraphicsFormatReader(_schemaFileName);
            var errors = new List<string>();

            foreach (var formatFileName in Directory.GetFiles(codecsPath).Where(x => x.EndsWith(".xml")))
            {
                var result = serializer.LoadFromFile(formatFileName);

                result.Switch(success =>
                    {
                        formats.Add(success.Result.Name, success.Result);
                    },
                    fail =>
                    {
                        errors.AddRange(fail.Reasons);
                    });
            }

            CodecFactory = new CodecFactory(formats, _defaultPalette);

            if (errors.Any())
                return new MagitekResults.Failed(errors);
            else
                return MagitekResults.SuccessResults;
        }

        public IEnumerable<string> GetSupportedCodecNames() => CodecFactory.GetSupportedCodecNames();
    }
}
