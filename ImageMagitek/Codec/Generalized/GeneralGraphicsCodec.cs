using System;
using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Advanced;
using ImageMagitek.Colors;
using ImageMagitek.ExtensionMethods;
using System.Collections.Generic;

namespace ImageMagitek.Codec
{
    /// <summary>
    /// GeneralGraphicsCodec provides a generalized method to encode/decode bitmap formats
    /// The process goes through several stages of transformations
    /// 
    /// For indexed bitmaps:
    /// 1. Deinterlace a bitmap's pixels into separate bitplanes
    /// 2. Merge bitplanes into indexed foreign colors
    /// 3. Create bitmap by translating foreign colors into local colors using a palette
    /// 4. Apply pixel remapping operations
    /// 
    /// For direct bitmaps:
    /// In development
    /// </summary>
    public class GeneralGraphicsCodec : IGraphicsCodec
    {
        public string Name { get; set; }
        public GraphicsFormat Format { get; private set; }
        public int StorageSize => Format.StorageSize;
        public ImageLayout Layout => Format.Layout;
        public PixelColorType ColorType => Format.ColorType;
        public int ColorDepth => Format.ColorDepth;
        public int Width => Format.Width;
        public int Height => Format.Height;
        public int RowStride => Format.RowStride;
        public int ElementStride => Format.ElementStride;
        public Palette DefaultPalette { get; set; }

        /// <summary>
        /// Preallocated buffer that separates and stores pixel color data
        /// </summary>
        private List<byte[]> ElementData;

        /// <summary>
        /// Preallocated buffer that stores merged pixel color data
        /// </summary>
        private byte[] MergedData;

        private byte[] _buffer;
        private Memory<byte> _memoryBuffer;
        private BitStream _bitStream;

        public GeneralGraphicsCodec(GraphicsFormat format, Palette defaultPalette)
        {
            Format = format;
            Name = format.Name;
            DefaultPalette = defaultPalette;
            AllocateBuffers();
        }

        private void AllocateBuffers()
        {
            ElementData = new List<byte[]>();
            for (int i = 0; i < Format.ColorDepth; i++)
            {
                byte[] data = new byte[Format.Width * Format.Height];
                ElementData.Add(data);
            }

            MergedData = new byte[Format.Width * Format.Height];

            _buffer = new byte[(StorageSize + 7) / 8];
            _memoryBuffer = new Memory<byte>(_buffer);
            _bitStream = BitStream.OpenRead(_buffer, StorageSize);
        }

        #region Graphics Decoding Functions
        /// <summary>
        /// General-purpose routine to decode a single arranger element
        /// </summary>
        /// <param name="image">Image to draw onto</param>
        /// <param name="el">ArrangerElement to decode</param>
        public void Decode(Image<Rgba32> image, ArrangerElement el)
        {
            if (Format.ColorType == PixelColorType.Indexed)
                IndexedDecode(image, el);
            else if (Format.ColorType == PixelColorType.Direct)
                DirectDecode(image, el);
        }

        /// <summary>
        /// Decoding routine to decode indexed (palette-based) graphics
        /// </summary>
        /// <param name="image">Destination bitmap</param>
        /// <param name="el">Element to decode</param>
        unsafe void IndexedDecode(Image<Rgba32> image, ArrangerElement el)
        {
            FileStream fs = el.DataFile.Stream;

            Format.Resize(el.Width, el.Height);

            if (el.FileAddress + Format.StorageSize > fs.Length * 8) // Element would contain data past the end of the file
                return;

            _bitStream.SeekAbsolute(0);
            fs.ReadUnshifted(el.FileAddress, Format.StorageSize, true, _memoryBuffer.Span);

            int plane = 0;
            int pos = 0;

            // Deinterlace into separate bitplanes
            foreach (ImageProperty ip in Format.ImageProperties)
            {
                pos = 0;
                if (ip.RowInterlace)
                {
                    for (int y = 0; y < el.Height; y++)
                    {
                        for (int curPlane = plane; curPlane < plane + ip.ColorDepth; curPlane++)
                        {
                            pos = y * el.Height;
                            for (int x = 0; x < el.Width; x++)
                                ElementData[Format.MergePriority[curPlane]][pos + ip.RowExtendedPixelPattern[x]] = (byte)_bitStream.ReadBit();
                        }
                    }
                }
                else // Non-interlaced
                {
                    for (int y = 0; y < el.Height; y++, pos += el.Width)
                        for (int x = 0; x < el.Width; x++)
                            for (int curPlane = plane; curPlane < plane + ip.ColorDepth; curPlane++)
                                ElementData[Format.MergePriority[curPlane]][pos + ip.RowExtendedPixelPattern[x]] = (byte)_bitStream.ReadBit();
                }

                plane += ip.ColorDepth;
            }

            // Merge into foreign pixel data
            byte foreignPixelData = 0;

            for (pos = 0; pos < MergedData.Length; pos++)
            {
                foreignPixelData = 0;
                for (int i = 0; i < Format.ColorDepth; i++)
                    foreignPixelData |= (byte)(ElementData[i][pos] << i); // Works for SNES palettes
                MergedData[pos] = foreignPixelData;
            }

            // Translate foreign colors to native colors and draw to bitmap
            DrawBitmapIndexedSafe(image, el);
        }

        public void DirectDecode(Image<Rgba32> image, ArrangerElement el)
        {
            throw new NotImplementedException();

            /*FileStream fs = FileManager.Instance.GetFileStream(el.FileName);
            GraphicsFormat format = FileManager.Instance.GetGraphicsFormat(el.FormatName);

            if (el.FileAddress + format.Size() > fs.Length * 8) // Element would contain data past the end of the file
            {
                DecodeBlank(image, el);
                return;
            }

            byte[] Data = fs.ReadUnshifted(el.FileAddress, format.Size(), true);
            // BitStream bs = BitStream.OpenRead(Data, format.Size()); // TODO: Change to account for first bit alignment

            int pos = 0;

            // Deinterlace into separate bitplanes
            foreach (ImageProperty ip in format.ImagePropertyList)
            {
                pos = 0;
                if (ip.RowInterlace)
                {
                    for (int y = 0; y < format.Height; y++)
                    {
                        for (int curPlane = plane; curPlane < plane + ip.ColorDepth; curPlane++)
                        {
                            pos = y * format.Height;
                            for (int x = 0; x < format.Width; x++)
                                el.TileData[curPlane][pos + ip.RowPixelPattern[x]] = (byte)bs.ReadBit();
                        }
                    }
                }
                else
                {
                    for (int y = 0; y < format.Height; y++, pos += format.Width)
                        for (int x = 0; x < format.Width; x++)
                            for (int curPlane = plane; curPlane < plane + ip.ColorDepth; curPlane++)
                                el.TileData[curPlane][pos + ip.RowPixelPattern[x]] = (byte)bs.ReadBit();
                }

                plane += ip.ColorDepth;
            }

            // Merge into foreign colors
            byte foreignColor = 0;

            for (pos = 0; pos < el.MergedData.Length; pos++)
            {
                foreignColor = 0;
                for (int i = 0; i < format.ColorDepth; i++)
                    //foreignColor |= (byte)(el.TileData[i][pos] << i); // Works for SNES palettes
                    foreignColor |= (byte)(el.TileData[i][pos] << (format.ColorDepth - i - 1)); // Works for TIM palettes
                el.MergedData[pos] = foreignColor;
            }

            // Translate foreign colors to local colors and draw to bitmap
            DrawBitmapIndexed(image, el);*/
        }

        void DrawBitmapIndexedSafe(Image<Rgba32> image, ArrangerElement el)
        {
            var dest = image.GetPixelSpan();

            int destidx = image.Width * el.Y1 + el.X1;
            int srcidx = 0;

            // Copy data into image
            for (int y = 0; y < Format.Height; y++)
            {
                for(int x = 0; x < Format.Width; x++, srcidx++, destidx++)
                {
                    var pal = el.Palette ?? DefaultPalette;
                    var nc = pal[MergedData[srcidx]];
                    var col = new Rgba32(nc.Color);
                    dest[destidx] = col;
                }
                destidx += el.X1 + image.Width - (el.X2 + 1);
            }
        }
        #endregion

        #region Graphics Encoding Functions
        public unsafe void Encode(Image<Rgba32> image, ArrangerElement el)
        {
            if (Format.ColorType == PixelColorType.Indexed)
                IndexedEncode(image, el);
            else if (Format.ColorType == PixelColorType.Direct)
                DirectEncode(image, el);
        }

        unsafe void IndexedEncode(Image<Rgba32> image, ArrangerElement el)
        {
            // ReadBitmap for local->foreign color conversion into fmt.MergedData
            ReadBitmapIndexedSafe(image, el);

            // Loop over MergedData to split foreign colors into bit planes in fmt.TileData
            for (int pos = 0; pos < MergedData.Length; pos++)
            {
                for (int i = 0; i < Format.ColorDepth; i++)
                    ElementData[i][pos] = (byte)((MergedData[pos] >> i) & 0x1);
            }

            // Loop over planes and putbit to data buffer with proper interlacing
            BitStream bs = BitStream.OpenWrite(Format.StorageSize, 8);
            int plane = 0;

            foreach (ImageProperty ip in Format.ImageProperties)
            {
                int pos = 0;

                if (ip.RowInterlace)
                {
                    for (int y = 0; y < Format.Height; y++)
                    {
                        for (int curPlane = plane; curPlane < plane + ip.ColorDepth; curPlane++)
                        {
                            pos = y * el.Height;
                            for (int x = 0; x < Format.Width; x++, pos++)
                                bs.WriteBit(ElementData[curPlane][pos]);
                            //for (int x = 0; x < el.Width; x++)
                            //    bs.WriteBit(el.ElementData[format.MergePriority[curPlane]][pos + ip.RowPixelPattern[x]]);
                        }
                    }
                }
                else
                {
                    for (int y = 0; y < Format.Height; y++, pos += Format.Width)
                    {
                        for (int x = 0; x < Format.Width; x++)
                            for (int curPlane = plane; curPlane < plane + ip.ColorDepth; curPlane++)
                                bs.WriteBit(ElementData[curPlane][pos + ip.RowPixelPattern[x]]);
                    }

                    /*for (int y = 0; y < el.Height; y++, pos += el.Width)
                    {
                        for (int x = 0; x < el.Width; x++)
                            for (int curPlane = plane; curPlane < plane + ip.ColorDepth; curPlane++)
                                bs.WriteBit(el.ElementData[format.MergePriority[curPlane]][pos + ip.RowPixelPattern[x]]);
                    }*/
                }

                plane += ip.ColorDepth;
            }

            el.DataFile.Stream.Seek(el.FileAddress.FileOffset, SeekOrigin.Begin);
            BinaryWriter bw = new BinaryWriter(el.DataFile.Stream);
            bw.Write(bs.Data, 0, bs.Data.Length); // TODO: Fix with a shifted, merged write
        }

        unsafe void DirectEncode(Image<Rgba32> image, ArrangerElement el)
        {
            throw new NotImplementedException();
        }

        void ReadBitmapIndexedSafe(Image<Rgba32> image, ArrangerElement el)
        {
            var src = image.GetPixelSpan();

            int srcidx = image.Width * el.Y1 + el.X1;
            int destidx = 0;

            // Copy data into element
            for (int y = 0; y < Format.Height; y++)
            {
                for (int x = 0; x < Format.Width; x++, srcidx++, destidx++)
                {
                    var col = src[srcidx];
                    var pal = el.Palette ?? DefaultPalette;
                    MergedData[destidx] = pal.GetIndexByNativeColor(new ColorRgba32(col.R, col.G, col.B, col.A), true);
                }
                srcidx += el.X1 + image.Width - (el.X2 + 1);
            }
        }

        /// <summary>
        /// Reads an indexed element at a specified location on a Rgba32 Bitmap
        /// </summary>
        /// <param name="image">Source bitmap</param>
        /// <param name="el">Destination arranger</param>
        unsafe void ReadBitmapIndexed(Image<Rgba32> image, ArrangerElement el)
        {
            /*Rectangle lockRect = new Rectangle(0, 0, image.Width, image.Height);
            BitmapData bd = image.LockBits(lockRect, ImageLockMode.WriteOnly, image.PixelFormat);

            // Read from bitmap
            uint* src = (uint*)bd.Scan0;
            int StrideWidth = bd.Stride - (image.Width * 4);

            src += (bd.Stride / 4) * el.Y1; // Seek to scanline PixelY in the bitmap

            fixed (byte* fixedData = el.MergedData)  // Fix fmt.MergedData in memory so unsafe pointers can be used
            {
                byte* dest = fixedData;

                int Height = el.Y2 - el.Y1 + 1;
                int Width = el.X2 - el.X1 + 1;
                Palette pal = ResourceManager.GetResource<Palette>(el.PaletteKey);

                for (int y = 0; y < Height; y++)
                {
                    src += el.X1; // Seek to PixelX in the scanline
                    for (int x = 0; x < Width; x++)
                    {
                        *dest = pal.GetIndexByNativeColor((NativeColor)(*src), true);
                        dest++;
                        src++;
                    }
                    src += (image.Width - el.X1 - Width);
                    src += StrideWidth;
                }
            }

            image.UnlockBits(bd);*/
        }

        #endregion
    }
}
