using System.Collections.Generic;
using System.Drawing;
using Xunit;

namespace ImageMagitek.UnitTests;

public partial class IndexedCodecContractTests
{
    private static readonly Dictionary<string, Size> _largeSizes = new()
    {
        ["PSX 4bpp"] = new Size(128, 64),
        ["PSX 8bpp"] = new Size(128, 64),
    };

    public static TheoryData<string, int, int> ContractCases => CodecTestHelpers.BuildCases(includeSquare: true, _largeSizes);
}