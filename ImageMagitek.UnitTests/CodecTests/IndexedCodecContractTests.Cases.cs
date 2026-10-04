using System.Collections.Generic;
using System.Drawing;
using Xunit;

namespace ImageMagitek.UnitTests;

public partial class IndexedCodecContractTests
{
    private static readonly Dictionary<string, Size> _largeSizes = new()
    {
        ["PSX 4bpp Flow"] = new Size(128, 64),
        ["PSX 8bpp Flow"] = new Size(128, 64),
    };

    public static TheoryData<string, int, int> ContractCases => CodecTestHelpers.BuildCases(knownBug: false, includeSquare: true, _largeSizes);

    public static TheoryData<string, int, int> KnownBugCases => CodecTestHelpers.BuildCases(knownBug: true, includeSquare: true, _largeSizes);

    /// <summary>
    /// Every case including those that hit the encode bug; decode-only tests may use these.
    /// </summary>
    public static TheoryData<string, int, int> AllCases => CodecTestHelpers.BuildCases(knownBug: null, includeSquare: true, _largeSizes);
}