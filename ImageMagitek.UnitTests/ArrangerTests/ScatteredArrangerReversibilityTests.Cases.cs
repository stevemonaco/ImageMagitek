using Xunit;

namespace ImageMagitek.UnitTests;

public partial class ScatteredArrangerReversibilityTests
{
    public static TheoryData<string, int, int> ReverseCases => CodecTestHelpers.BuildCases(knownBug: false, includeSquare: false);

    public static TheoryData<string, int, int> KnownBugReverseCases => CodecTestHelpers.BuildCases(knownBug: true, includeSquare: false);
}