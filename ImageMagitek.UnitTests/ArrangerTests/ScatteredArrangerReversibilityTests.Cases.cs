using Xunit;

namespace ImageMagitek.UnitTests;

public partial class ScatteredArrangerReversibilityTests
{
    public static TheoryData<string, int, int> ReverseCases => CodecTestHelpers.BuildCases(includeSquare: false);
}