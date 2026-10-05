using ImageMagitek.Colors;
using ImageMagitek.Colors.Serialization;
using Xunit;

namespace ImageMagitek.UnitTests.PaletteEditorTests;

public class PaletteFileSerializerTests
{
    private static readonly ColorRgba32[] _colors =
    [
        new(0, 0, 0, 255),
        new(255, 128, 1, 255),
        new(12, 34, 56, 255),
    ];

    [Fact]
    public void Jasc_RoundTrips()
    {
        var text = PaletteFileSerializer.WriteJasc(_colors);

        var result = PaletteFileSerializer.Read(text);

        Assert.True(result.HasSucceeded);
        Assert.Equal(_colors, result.AsSuccess.Result);
        Assert.StartsWith("JASC-PAL", text);
    }

    [Fact]
    public void Gpl_RoundTrips()
    {
        var text = PaletteFileSerializer.WriteGpl("Test", _colors);

        var result = PaletteFileSerializer.Read(text);

        Assert.True(result.HasSucceeded);
        Assert.Equal(_colors, result.AsSuccess.Result);
        Assert.StartsWith("GIMP Palette", text);
    }

    [Fact]
    public void Read_UnknownHeader_Fails()
    {
        var result = PaletteFileSerializer.Read("RIFF palette\n1 2 3\n");

        Assert.True(result.HasFailed);
        Assert.Contains("Line 1", result.AsError.Reason);
    }

    [Fact]
    public void Read_JascBadEntry_NamesLine()
    {
        var result = PaletteFileSerializer.Read("JASC-PAL\r\n0100\r\n2\r\n1 2 3\r\n1 300 3\r\n");

        Assert.True(result.HasFailed);
        Assert.Contains("Line 5", result.AsError.Reason);
    }

    [Fact]
    public void Read_JascTooFewEntries_Fails()
    {
        var result = PaletteFileSerializer.Read("JASC-PAL\n0100\n3\n1 2 3\n");

        Assert.True(result.HasFailed);
    }

    [Fact]
    public void Read_GplBadEntry_NamesLine()
    {
        var result = PaletteFileSerializer.Read("GIMP Palette\nName: x\n#\n  1   2   3\tA\nnot a color\n");

        Assert.True(result.HasFailed);
        Assert.Contains("Line 5", result.AsError.Reason);
    }
}
