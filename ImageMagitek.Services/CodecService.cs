using System.Collections.Generic;
using System.IO;
using System.Linq;
using ImageMagitek.Codec;
using ImageMagitek.Colors;

namespace ImageMagitek.Services
{
    public interface ICodecService
    {
        ICodecFactory CodecFactory { get; }

        IEnumerable<string> GetSupportedCodecNames();
        MagitekResults LoadXmlCodecs(string codecsPath);
    }

    public class CodecService : ICodecService
    {
        public ICodecFactory CodecFactory { get; private set; }

        private readonly string _schemaFileName;

        public CodecService(string schemaFileName)
        {
            _schemaFileName = schemaFileName;
        }

        public MagitekResults LoadXmlCodecs(string codecsPath)
        {
            var formats = new Dictionary<string, IGraphicsFormat>();
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

            CodecFactory = new CodecFactory(formats);

            if (errors.Any())
                return new MagitekResults.Failed(errors);
            else
                return MagitekResults.SuccessResults;
        }

        public IEnumerable<string> GetSupportedCodecNames() => CodecFactory?.GetSupportedCodecNames();
    }
}
