using ImageMagitek.Colors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;
using System;

namespace ImageMagitek.Codec
{
    //public sealed class BlankCodec : IDirectGraphicsCodec, IIndexedCodec
    //{
    //    public string Name => "Blank";

    //    public int StorageSize => 0;
    //    public int Width => 0;
    //    public int Height => 0;
    //    public ImageLayout Layout => ImageLayout.Tiled;
    //    public PixelColorType ColorType => PixelColorType.Direct;
    //    public int ColorDepth => 1;
    //    public int RowStride => 0;
    //    public int ElementStride => 0;

    //    public ReadOnlySpan<byte> ForeignBuffer => _foreignBuffer;
    //    private byte[] _foreignBuffer;
    //    public byte[,] NativeBuffer => throw new NotImplementedException();

    //    private Rgba32 _fillColor = Rgba32.Black;
    //    private byte _fillIndex = 0;

    //    public BlankCodec() { }

    //    public BlankCodec(Rgba32 fillColor, byte fillIndex)
    //    {
    //        _fillColor = fillColor;
    //        _fillIndex = fillIndex;
    //    }

    //    public void Decode(Image<Rgba32> image, ArrangerElement el)
    //    {
    //        var dest = image.GetPixelSpan();
    //        int destidx = image.Width * el.Y1 + el.X1;

    //        for (int y = 0; y < el.Height; y++)
    //        {
    //            for (int x = 0; x < el.Width; x++, destidx++)
    //                dest[destidx] = _fillColor;
    //            destidx += el.X1 + image.Width - (el.X2 + 1);
    //        }
    //    }

    //    public void Encode(Image<Rgba32> image, ArrangerElement el) { }

    //    public void Decode(ArrangerElement el, ColorRgba32[,] imageBuffer)
    //    {
    //        var color = new ColorRgba32(_fillColor.R, _fillColor.G, _fillColor.B, _fillColor.A);

    //        for(int y = 0; y < el.Height; y++)
    //        {
    //            for (int x = 0; x < el.Width; x++)
    //                imageBuffer[x, y] = color;
    //        }
    //    }

    //    public void Encode(ArrangerElement el, ColorRgba32[,] imageBuffer) { }

    //    public void Decode(ArrangerElement el, byte[,] imageBuffer)
    //    {
    //        for (int y = 0; y < el.Height; y++)
    //        {
    //            for (int x = 0; x < el.Width; x++)
    //                imageBuffer[x, y] = _fillIndex;
    //        }
    //    }

    //    public void Encode(ArrangerElement el, byte[,] imageBuffer) { }

    //    public ReadOnlySpan<byte> ReadElement(ArrangerElement el)
    //    {
    //        throw new NotImplementedException();
    //    }

    //    public void WriteElement(ArrangerElement el, ReadOnlySpan<byte> encodedBuffer) { }

    //    public byte[,] DecodeElement(ArrangerElement el, ReadOnlySpan<byte> encodedBuffer)
    //    {
    //        throw new NotImplementedException();
    //    }

    //    public ReadOnlySpan<byte> EncodeElement(ArrangerElement el, byte[,] imageBuffer)
    //    {
    //        throw new NotImplementedException();
    //    }
    //}
}
