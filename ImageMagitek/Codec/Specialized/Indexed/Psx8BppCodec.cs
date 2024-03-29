﻿using System;
using ImageMagitek.Colors;

namespace ImageMagitek.Codec;
public sealed class Psx8BppCodec : IndexedCodec
{
    public override string Name => "PSX 8bpp";
    public override ImageLayout Layout => ImageLayout.Single;
    public override int ColorDepth => 8;
    public override int StorageSize => Width * Height * 8;
    public override bool CanEncode => true;

    public override int DefaultWidth => 64;
    public override int DefaultHeight => 64;
    public override int WidthResizeIncrement => 1;
    public override int HeightResizeIncrement => 1;
    public override bool CanResize => true;

    public Psx8BppCodec(Palette palette) : base(palette)
    {
    }

    public Psx8BppCodec(Palette palette, int width, int height) : base(palette, width, height)
    {
    }

    public override byte[,] DecodeElement(in ArrangerElement el, ReadOnlySpan<byte> encodedBuffer)
    {
        if (encodedBuffer.Length * 8 < StorageSize) // Decoding would require data past the end of the buffer
            throw new ArgumentException(nameof(encodedBuffer));

        int srcidx = 0;

        for (int y = 0; y < el.Height; y++)
        {
            for (int x = 0; x < el.Width; x++, srcidx++)
            {
                var palIndex = encodedBuffer[srcidx];
                _nativeBuffer[y, x] = palIndex;
            }
        }

        return _nativeBuffer;
    }

    public override ReadOnlySpan<byte> EncodeElement(in ArrangerElement el, byte[,] imageBuffer)
    {
        if (imageBuffer.GetLength(0) != Height || imageBuffer.GetLength(1) != Width)
            throw new ArgumentException(nameof(imageBuffer));

        int destidx = 0;

        for (int y = 0; y < el.Height; y++)
        {
            for (int x = 0; x < el.Width; x++, destidx++)
            {
                byte index = imageBuffer[y, x];
                _foreignBuffer[destidx] = index;
            }
        }

        return ForeignBuffer;
    }
}
