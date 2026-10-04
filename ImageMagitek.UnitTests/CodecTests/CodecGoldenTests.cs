using System.Text;
using System.Threading.Tasks;
using ImageMagitek.UnitTests.Fixtures;
using VerifyXunit;
using Xunit;

namespace ImageMagitek.UnitTests;

/// <summary>
/// Pins encoded bytes and decoded indices as Verify snapshots in Snapshots/*.verified.txt.
/// To accept intentional changes, review the generated *.received.txt files and rename them to *.verified.txt.
/// </summary>
[Collection("Codec")]
public class CodecGoldenTests
{
    private readonly CodecFixture _fixture;

    public CodecGoldenTests(CodecFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory]
    [MemberData(nameof(IndexedCodecContractTests.AllCases), MemberType = typeof(IndexedCodecContractTests))]
    public Task Codec_MatchesSnapshot(string codecName, int width, int height)
    {
        var codec = CodecTestHelpers.CreateCodec(_fixture.CodecFactory, codecName, width, height);
        var el = CodecTestHelpers.CreateElement(codec);
        var skipEncode = CodecTestHelpers.HitsRowInterlaceEncodeBug(_fixture.CodecFactory, codecName, width, height);

        var sb = new StringBuilder();
        sb.Append($"codec: {codecName}\nsize: {width}x{height}\ncolorDepth: {codec.ColorDepth}\nstorageBits: {codec.StorageSize}\n");

        if (skipEncode)
        {
            sb.Append("\nencode: skipped, ").Append(CodecTestHelpers.RowInterlaceEncodeBug).Append('\n');
        }
        else
        {
            var gradient = TestImageGenerator.GradientIndices(width, height, codec.ColorDepth);
            sb.Append("\nencode gradient:\n").Append(CodecTestHelpers.ToHex(CodecTestHelpers.Encode(codec, el, gradient)));

            var random = TestImageGenerator.RandomIndices(width, height, codec.ColorDepth, 101);
            sb.Append("\nencode random indices (seed 101):\n").Append(CodecTestHelpers.ToHex(CodecTestHelpers.Encode(codec, el, random)));
        }

        var bytes = TestImageGenerator.RandomBytes((codec.StorageSize + 7) / 8, 202);
        var decoded = CodecTestHelpers.Decode(codec, el, bytes);
        sb.Append("\ndecode random bytes (seed 202):\n").Append(CodecTestHelpers.ToIndexRows(decoded, codec.ColorDepth));

        return Verifier.Verify(sb.ToString())
            .UseDirectory("Snapshots")
            .UseFileName($"{codecName}_{width}x{height}");
    }
}
