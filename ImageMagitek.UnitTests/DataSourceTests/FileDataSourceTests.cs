using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace ImageMagitek.UnitTests.DataSourceTests;

public sealed class FileDataSourceTests : IDisposable
{
    private static readonly byte[] _sentinel = TestImageGenerator.RandomBytes(16, 73);
    private readonly TempDataFiles _files = new();

    [Fact]
    public void ReadOnlyAttribute_OpensAndReads()
    {
        var source = _files.Open("test", _sentinel, isReadOnly: true);

        Assert.Equal(_sentinel, CodecTestHelpers.ReadAll(source));
    }

    [Fact]
    public void ReadOnlyAttribute_IsReadOnly()
    {
        var source = _files.Open("test", _sentinel, isReadOnly: true);

        Assert.True(source.IsReadOnly);
    }

    [Fact]
    public void WritableFile_IsNotReadOnly()
    {
        var source = _files.Open("test", _sentinel, isReadOnly: false);

        Assert.False(source.IsReadOnly);
    }

    [Fact]
    public void MissingFile_IsNotReadOnly()
    {
        var source = _files.Open("test", TestPaths.CreateTempPath(".bin"));

        Assert.False(source.IsReadOnly);
    }

    [Fact]
    public async Task ReadOnly_EveryWriteOverload_ThrowsAndLeavesBytes()
    {
        var path = _files.Create(_sentinel);
        var source = _files.Open("test", path);
        var data = new byte[] { 1, 2, 3, 4 };

        Assert.Throws<InvalidOperationException>(() => source.Write(data));
        Assert.Throws<InvalidOperationException>(() => source.Write(BitAddress.Zero, data));
        Assert.Throws<InvalidOperationException>(() => source.Write(new BitAddress(0, 3), 12, data));
        await Assert.ThrowsAsync<InvalidOperationException>(() => source.WriteAsync(data));
        await Assert.ThrowsAsync<InvalidOperationException>(() => source.WriteAsync(BitAddress.Zero, data));
        await Assert.ThrowsAsync<InvalidOperationException>(() => source.WriteAsync(new BitAddress(0, 3), 12, data));
        source.Flush();
        await source.FlushAsync();

        Assert.Equal(_sentinel, CodecTestHelpers.ReadAll(source));
        source.Dispose();
        Assert.Equal(_sentinel, File.ReadAllBytes(path));
    }

    [Fact]
    public void Reopen_AfterClearingAttribute_IsWritable()
    {
        var path = _files.Create(_sentinel);
        var source = _files.Open("test", path);
        Assert.True(source.IsReadOnly);

        File.SetAttributes(path, FileAttributes.Normal);
        source.Reopen();

        Assert.False(source.IsReadOnly);
        source.Write(BitAddress.Zero, new byte[] { 0xAB });
        Assert.Equal(0xAB, source.Read(BitAddress.Zero, 8)[0]);
    }

    public void Dispose() => _files.Dispose();
}
