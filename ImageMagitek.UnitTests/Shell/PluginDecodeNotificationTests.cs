using System;
using ImageMagitek.Codec;
using ImageMagitek.Plugins;
using Microsoft.Extensions.Logging.Abstractions;
using TileShop.Shared.Models;
using TileShop.Shared.Services;
using TileShop.UI;
using Xunit;

namespace ImageMagitek.UnitTests.Shell;

public class PluginDecodeNotificationTests
{
    [Fact]
    public void DecodeFailed_TwoAdapters_PostsOneWarningWithCountTwo()
    {
        var service = new NotificationService(NullLogger<NotificationService>.Instance, a => a());
        void OnDecodeFailed(string name, Exception ex)
        {
            if (name == ThrowingDecodePlugin.Name)
                TileShopBootstrapper.PostDecodeFailure(service, name, ex);
        }

        CodecPluginEvents.DecodeFailed += OnDecodeFailed;
        try
        {
            var palette = TestImageGenerator.CreateDistinctPalette(2);
            foreach (var codec in new[] { new IndexedCodecPluginAdapter(new ThrowingDecodePlugin(), palette), new IndexedCodecPluginAdapter(new ThrowingDecodePlugin(), palette) })
                codec.DecodeElement(CodecTestHelpers.CreateElement(codec), new byte[8]);

            var notification = Assert.Single(service.Notifications);
            Assert.Equal(NotificationSeverity.Warning, notification.Severity);
            Assert.Equal(2, notification.Count);
            Assert.Contains(ThrowingDecodePlugin.Name, notification.Title);
            Assert.Equal("Decode failure", notification.Detail);
        }
        finally
        {
            CodecPluginEvents.DecodeFailed -= OnDecodeFailed;
        }
    }

    private sealed class ThrowingDecodePlugin : IIndexedCodecPlugin
    {
        public const string Name = "Notification Throwing Decode";

        public CodecInfo Info { get; } = new() { Name = Name, Layout = CodecLayout.Tiled, ColorDepth = 1, DefaultWidth = 8, DefaultHeight = 8 };
        public int GetStorageBits(int width, int height) => width * height;
        public void Decode(ReadOnlySpan<byte> encoded, Span<byte> pixels, int width, int height) => throw new FormatException("Decode failure");
        public void Encode(ReadOnlySpan<byte> pixels, Span<byte> encoded, int width, int height) { }
    }
}
