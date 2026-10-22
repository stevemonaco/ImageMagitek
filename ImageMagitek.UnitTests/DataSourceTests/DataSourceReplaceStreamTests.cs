using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ImageMagitek.UnitTests.DataSourceTests;

public sealed class DataSourceReplaceStreamTests : IDisposable
{
    private readonly TempDataFiles _files = new();

    public void Dispose() => _files.Dispose();

    [Fact]
    public async Task ReplaceStream_WaitsForInFlightRead()
    {
        byte[] original = [1, 2, 3, 4];
        using var source = new BlockingDataSource(original);

        var read = Task.Run(() => source.Read(BitAddress.Zero, original.Length * 8));
        Assert.True(source.ReadEntered.Wait(TimeSpan.FromSeconds(5)));

        var replace = Task.Run(source.Replace);
        await Task.Delay(100);
        Assert.False(replace.IsCompleted);

        source.ReleaseRead.Set();

        Assert.Equal(original, await read.WaitAsync(TimeSpan.FromSeconds(5)));
        await replace.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Reopen_DuringConcurrentReads_NeverThrows()
    {
        var bytes = TestImageGenerator.RandomBytes(64, 11);
        var source = _files.Open("test", bytes, isReadOnly: false);

        var reader = Task.Run(() =>
        {
            for (int i = 0; i < 200; i++)
                Assert.Equal(bytes, CodecTestHelpers.ReadAll(source));
        });
        var reopener = Task.Run(() =>
        {
            for (int i = 0; i < 200; i++)
                source.Reopen();
        });

        await Task.WhenAll(reader, reopener).WaitAsync(TimeSpan.FromSeconds(30));
    }

    private sealed class BlockingDataSource : DataSource
    {
        private Lazy<Stream> _stream;
        private readonly byte[] _bytes;

        public ManualResetEventSlim ReadEntered { get; } = new();
        public ManualResetEventSlim ReleaseRead { get; } = new();

        protected override Lazy<Stream> Stream => _stream;

        public BlockingDataSource(byte[] bytes) : base("blocking")
        {
            _bytes = bytes;
            _stream = CreateStream();
        }

        public void Replace() => ReplaceStream(() => _stream = CreateStream());

        private Lazy<Stream> CreateStream() => new(() => new BlockingStream(_bytes, this));

        private sealed class BlockingStream(byte[] bytes, BlockingDataSource owner) : MemoryStream(bytes, writable: false)
        {
            public override int Read(byte[] buffer, int offset, int count)
            {
                owner.ReadEntered.Set();
                owner.ReleaseRead.Wait();
                return base.Read(buffer, offset, count);
            }
        }
    }
}
