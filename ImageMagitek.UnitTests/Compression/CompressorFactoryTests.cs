using System;
using System.Linq;
using ImageMagitek.Compression;
using ImageMagitek.Plugins;
using Xunit;

namespace ImageMagitek.UnitTests.Compression;

public class CompressorFactoryTests
{
    [Theory]
    [InlineData(typeof(AbstractCompression), "is abstract")]
    [InlineData(typeof(string), "does not implement")]
    [InlineData(typeof(NoConstructorCompression), "parameterless constructor")]
    [InlineData(typeof(ThrowingCompression), "could not be created")]
    [InlineData(typeof(BlankNameCompression), "is blank")]
    public void AddCompressionPlugin_InvalidType_FailsWithReason(Type type, string reason)
    {
        var factory = new CompressorFactory();

        var result = factory.AddCompressionPlugin(type);

        Assert.True(result.HasFailed);
        Assert.Contains(reason, result.AsError.Reason);
        Assert.Contains(type.Name, result.AsError.Reason);
    }

    [Fact]
    public void AddCompressionPlugin_NameCollision_KeepsFirst()
    {
        var factory = new CompressorFactory();

        var result = factory.AddCompressionPlugin(typeof(CollidingCompression));

        Assert.True(result.HasFailed);
        Assert.Contains("GBA LZ77", result.AsError.Reason);
        Assert.IsType<GbaLz77Compression>(factory.CreateCompressor("gba lz77"));
    }

    [Fact]
    public void Create_KnownAndUnknownNames()
    {
        var factory = new CompressorFactory();
        Assert.True(factory.AddCompressionPlugin(typeof(TestCompression)).HasSucceeded);

        var first = factory.CreateCompressor("Test");
        var second = factory.CreateCompressor("Test");

        Assert.IsType<TestCompression>(first);
        Assert.NotSame(first, second);
        Assert.Null(factory.CreateCompressor("Missing"));
        Assert.Equal(["GBA LZ77", "GBA RLE", "Test"], factory.GetRegisteredCompressors().Select(x => x.Name));
    }

    public abstract class AbstractCompression : ICompressionPlugin
    {
        public CompressionInfo Info { get; } = new() { Name = "Abstract" };
        public DecompressionResult Decompress(ReadOnlySpan<byte> input) => new([], 0);
    }

    public sealed class NoConstructorCompression(int value) : ICompressionPlugin
    {
        public CompressionInfo Info { get; } = new() { Name = $"NoConstructor{value}" };
        public DecompressionResult Decompress(ReadOnlySpan<byte> input) => new([], 0);
    }

    public sealed class ThrowingCompression : ICompressionPlugin
    {
        public ThrowingCompression() => throw new InvalidOperationException("boom");
        public CompressionInfo Info => new() { Name = "Throwing" };
        public DecompressionResult Decompress(ReadOnlySpan<byte> input) => new([], 0);
    }

    public sealed class BlankNameCompression : ICompressionPlugin
    {
        public CompressionInfo Info { get; } = new() { Name = " " };
        public DecompressionResult Decompress(ReadOnlySpan<byte> input) => new([], 0);
    }

    public sealed class CollidingCompression : ICompressionPlugin
    {
        public CompressionInfo Info { get; } = new() { Name = "GBA LZ77" };
        public DecompressionResult Decompress(ReadOnlySpan<byte> input) => new([], 0);
    }

    public sealed class TestCompression : ICompressionPlugin
    {
        public CompressionInfo Info { get; } = new() { Name = "Test" };
        public DecompressionResult Decompress(ReadOnlySpan<byte> input) => new(input.ToArray(), input.Length);
    }
}
