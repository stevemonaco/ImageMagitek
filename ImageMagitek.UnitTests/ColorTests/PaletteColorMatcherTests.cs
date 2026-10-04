using ImageMagitek.Colors;
using ImageMagitek.UnitTests.TestFactories;
using Xunit;

namespace ImageMagitek.UnitTests.ColorTests;

public class PaletteColorMatcherTests
{
    private static readonly ColorRgba32 _black = new(0, 0, 0, 255);
    private static readonly ColorRgba32 _white = new(255, 255, 255, 255);
    private static readonly ColorRgba32 _red = new(255, 0, 0, 255);
    private static readonly ColorRgba32 _blue = new(0, 0, 255, 255);

    private static Palette CreatePalette() => ArrangerTestFactory.CreatePalette(_black, _white, _red, _blue);

    [Theory]
    [InlineData(ColorMatchStrategy.Exact)]
    [InlineData(ColorMatchStrategy.Nearest)]
    [InlineData(ColorMatchStrategy.NearestRgb)]
    public void TryMatch_IdenticalColor_IsExactHit(ColorMatchStrategy strategy)
    {
        var matcher = new PaletteColorMatcher(CreatePalette(), strategy);

        var isMatched = matcher.TryMatch(_red, out var match);

        Assert.True(isMatched);
        Assert.Equal(new ColorMatch(2, 0, true), match);
    }

    [Fact]
    public void TryMatch_Exact_MissingColor_RejectsWithNearestHint()
    {
        var matcher = new PaletteColorMatcher(CreatePalette(), ColorMatchStrategy.Exact);

        var isMatched = matcher.TryMatch(new ColorRgba32(250, 10, 10, 255), out var match);

        Assert.False(isMatched);
        Assert.Equal(2, match.Index);
        Assert.False(match.IsExact);
        Assert.True(match.Distance > 0);
    }

    [Fact]
    public void TryMatch_Exact_DuplicateEntries_FirstWins()
    {
        var matcher = new PaletteColorMatcher(ArrangerTestFactory.CreatePalette(_black, _red, _red), ColorMatchStrategy.Exact);

        matcher.TryMatch(_red, out var match);

        Assert.Equal(1, match.Index);
    }

    [Theory]
    [InlineData(ColorMatchStrategy.Nearest)]
    [InlineData(ColorMatchStrategy.NearestRgb)]
    public void TryMatch_Nearest_PicksClosestEntry(ColorMatchStrategy strategy)
    {
        var matcher = new PaletteColorMatcher(CreatePalette(), strategy);

        var isMatched = matcher.TryMatch(new ColorRgba32(20, 20, 230, 255), out var match);

        Assert.True(isMatched);
        Assert.Equal(3, match.Index);
        Assert.False(match.IsExact);
    }

    [Fact]
    public void TryMatch_Nearest_IgnoresAlpha()
    {
        var matcher = new PaletteColorMatcher(CreatePalette(), ColorMatchStrategy.Nearest);

        var isMatched = matcher.TryMatch(new ColorRgba32(255, 0, 0, 0), out var match);

        Assert.True(isMatched);
        Assert.Equal(2, match.Index);
        Assert.False(match.IsExact);
        Assert.Equal(0, match.Distance);
    }

    [Fact]
    public void TryMatch_Nearest_BeyondMaxDistance_RejectsWithHint()
    {
        var matcher = new PaletteColorMatcher(CreatePalette(), ColorMatchStrategy.Nearest, maxDistance: 1);

        var isMatched = matcher.TryMatch(new ColorRgba32(0, 200, 0, 255), out var match);

        Assert.False(isMatched);
        Assert.True(match.Distance > 1);
    }

    [Fact]
    public void TryMatch_EntryLimit_ExcludesLaterEntries()
    {
        var exact = new PaletteColorMatcher(CreatePalette(), ColorMatchStrategy.Exact, entryLimit: 2);
        var nearest = new PaletteColorMatcher(CreatePalette(), ColorMatchStrategy.Nearest, entryLimit: 2);

        Assert.False(exact.TryMatch(_blue, out _));
        Assert.True(nearest.TryMatch(_blue, out var match));
        Assert.True(match.Index < 2);
    }

    [Fact]
    public void TryMatch_EmptyPalette_Rejects()
    {
        var matcher = new PaletteColorMatcher(ArrangerTestFactory.CreatePalette(), ColorMatchStrategy.Nearest);

        Assert.False(matcher.TryMatch(_red, out _));
    }

    [Fact]
    public void TryMatch_RepeatedColor_ReturnsSameResult()
    {
        var matcher = new PaletteColorMatcher(CreatePalette(), ColorMatchStrategy.Nearest);
        var color = new ColorRgba32(30, 30, 30, 255);

        matcher.TryMatch(color, out var first);
        matcher.TryMatch(color, out var second);

        Assert.Equal(first, second);
    }
}
