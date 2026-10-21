using System.Collections.Generic;
using ImageMagitek.Tables;
using Xunit;

namespace ImageMagitek.UnitTests.Tables;

public sealed class TableExpressionTests
{
    private sealed class Scope(Dictionary<string, long> values) : IExpressionScope
    {
        public long GetValue(string identifier) => values[identifier];
        public long GetContextValue(string field) => throw new TableEvaluationException("no context");
        public long GetLookupValue(string lookup, long index, string field) => index * 10;
    }

    private static long Evaluate(string text, Dictionary<string, long>? values = null)
    {
        var result = TableExpression.Parse(text);
        Assert.True(result.HasSucceeded, result.HasFailed ? result.AsError.Reason : null);
        return result.AsSuccess.Result.Evaluate(new Scope(values ?? []));
    }

    [Theory]
    [InlineData("1 + 2 * 3", 7)]
    [InlineData("(1 + 2) * 3", 9)]
    [InlineData("10 - 4 - 3", 3)]
    [InlineData("100 / 10 / 5", 2)]
    [InlineData("1 + 2 << 3", 24)]
    [InlineData("1 << 2 + 1", 8)]
    [InlineData("6 & 3 | 8", 10)]
    [InlineData("1 | 6 ^ 3", 5)]
    [InlineData("2 < 3 == 1", 1)]
    [InlineData("4 & 3 == 3", 0)]
    [InlineData("1 + 2 == 3 & 5 > 4", 1)]
    [InlineData("-2 * -3", 6)]
    [InlineData("~0 & 0xFF", 255)]
    [InlineData("-7 / 2", -3)]
    [InlineData("-7 % 3", -1)]
    public void Precedence(string text, long expected)
    {
        Assert.Equal(expected, Evaluate(text));
    }

    [Fact]
    public void HexAndDecimal()
    {
        Assert.Equal(0x48000 + (0x8123 & 0x7FFF) * 8, Evaluate("0x48000 + (pointer & 0x7FFF) * 8", new() { ["pointer"] = 0x8123 }));
        Assert.Equal(255, Evaluate("0xff"));
        Assert.Equal(10, Evaluate("010"));
    }

    [Fact]
    public void MinMax()
    {
        Assert.Equal(384, Evaluate("min(384, gap / 32)", new() { ["gap"] = 0x4000 }));
        Assert.Equal(-1, Evaluate("min(3, -1, 7)"));
        Assert.Equal(7, Evaluate("max(3, -1, 7)"));
        Assert.Equal(5, Evaluate("max(5)"));
    }

    [Fact]
    public void Ternary()
    {
        Assert.Equal(640, Evaluate("isSecondary ? 384 : 640", new() { ["isSecondary"] = 0 }));
        Assert.Equal(384, Evaluate("isSecondary ? 384 : 640", new() { ["isSecondary"] = 2 }));
        Assert.Equal(3, Evaluate("0 ? 1 : 0 ? 2 : 3"));
        Assert.Equal(12, Evaluate("1 ? 1 ? 12 : 13 : 14"));
        Assert.Equal(30, Evaluate("sizes[1 + 2].width"));
    }

    [Fact]
    public void ShiftsAndBitwise()
    {
        Assert.Equal(0x80, Evaluate("1 << 7"));
        Assert.Equal(-4, Evaluate("-16 >> 2"));
        Assert.Equal(6, Evaluate("5 ^ 3"));
        Assert.Equal(1, Evaluate("(size & 0x80) != 0", new() { ["size"] = 0x81 }));

        var shift = TableExpression.Parse("1 << 64").AsSuccess.Result.TryEvaluate(new Scope([]));
        Assert.True(shift.HasFailed);
        Assert.Contains("shift count 64", shift.AsError.Reason);
    }

    [Fact]
    public void DivisionByZero_IsEvaluationError()
    {
        var result = TableExpression.Parse("width / (height - 2)").AsSuccess.Result.TryEvaluate(new Scope(new() { ["width"] = 4, ["height"] = 2 }));

        Assert.True(result.HasFailed);
        Assert.Contains("division by zero", result.AsError.Reason);
        Assert.Contains("width / (height - 2)", result.AsError.Reason);
    }

    [Fact]
    public void ModuloByZero_IsEvaluationError()
    {
        var result = TableExpression.Parse("7 % 0").AsSuccess.Result.TryEvaluate(new Scope([]));

        Assert.True(result.HasFailed);
        Assert.Contains("modulo by zero", result.AsError.Reason);
    }
}
