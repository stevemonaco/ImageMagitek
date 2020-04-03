using System;
using System.Collections.Generic;
using System.IO;
using ImageMagitek.Colors;
using ImageMagitek.ExtensionMethods;

namespace ImageMagitek.Codec
{
    public class IndexedGraphicsCodec : IIndexedCodec
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

        public virtual ReadOnlySpan<byte> ForeignBuffer => _foreignBuffer;
        protected byte[] _foreignBuffer;

        public virtual byte[,] NativeBuffer => _nativeBuffer;
        protected byte[,] _nativeBuffer;

        /// <summary>
        /// Preallocated buffer that separates and stores pixel color data
        /// </summary>
        private List<byte[]> ElementData;

        /// <summary>
        /// Preallocated buffer that stores merged pixel color data
        /// </summary>
        private byte[] MergedData;

        private BitStream _bitStream;

        public IndexedGraphicsCodec(GraphicsFormat format, Palette defaultPalette)
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

            _foreignBuffer = new byte[(StorageSize + 7) / 8];
            _nativeBuffer = new byte[Width, Height];

            _bitStream = BitStream.OpenRead(_foreignBuffer, StorageSize);
        }

        public byte[,] DecodeElement(ArrangerElement el, ReadOnlySpan<byte> encodedBuffer)
        {
            if (encodedBuffer.Length * 8 < StorageSize) // Decoding would require data past the end of the buffer
                throw new ArgumentException(nameof(encodedBuffer));

            encodedBuffer.Slice(0, _foreignBuffer.Length).CopyTo(_foreignBuffer);
            _bitStream.SeekAbsolute(0);

            int plane = 0;
            int pos;

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
                                ElementData[Format.MergePlanePriority[curPlane]][pos + ip.RowPixelPattern[x]] = (byte)_bitStream.ReadBit();
                        }
                    }
                }
                else // Non-interlaced
                {
                    for (int y = 0; y < el.Height; y++, pos += el.Width)
                        for (int x = 0; x < el.Width; x++)
                            for (int curPlane = plane; curPlane < plane + ip.ColorDepth; curPlane++)
                                ElementData[Format.MergePlanePriority[curPlane]][pos + ip.RowPixelPattern[x]] = (byte)_bitStream.ReadBit();
                }

                plane += ip.ColorDepth;
            }

            // Merge into foreign pixel data
            byte foreignPixelData;

            for (pos = 0; pos < MergedData.Length; pos++)
            {
                foreignPixelData = 0;
                for (int i = 0; i < Format.ColorDepth; i++)
                    foreignPixelData |= (byte)(ElementData[i][pos] << i); // Works for SNES image data and palettes, may need customization later
                MergedData[pos] = foreignPixelData;
            }

            pos = 0;
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++, pos++)
                    _nativeBuffer[x, y] = MergedData[pos];

            return NativeBuffer;
        }

        /// <summary>
        /// Decoding routine to decode indexed (palette-based) graphics
        /// </summary>
        /// <param name="el">Element to decode</param>
        /// <param name="imageBuffer">Caller-allocated buffer to hold resultant indexed pixel data</param>
        //public unsafe void Decode(ArrangerElement el, byte[,] imageBuffer)
        //{
        //    FileStream fs = el.DataFile.Stream;

        //    Format.Resize(el.Width, el.Height);

        //    if (el.FileAddress + Format.StorageSize > fs.Length * 8) // Element would contain data past the end of the file
        //        return;

        //    _bitStream.SeekAbsolute(0);
        //    //fs.ReadUnshifted(el.FileAddress, Format.StorageSize, true, _bitStreamMemory.Span);

        //    int plane = 0;
        //    int pos;

        //    // Deinterlace into separate bitplanes
        //    foreach (ImageProperty ip in Format.ImageProperties)
        //    {
        //        pos = 0;
        //        if (ip.RowInterlace)
        //        {
        //            for (int y = 0; y < el.Height; y++)
        //            {
        //                for (int curPlane = plane; curPlane < plane + ip.ColorDepth; curPlane++)
        //                {
        //                    pos = y * el.Height;
        //                    for (int x = 0; x < el.Width; x++)
        //                        ElementData[Format.MergePlanePriority[curPlane]][pos + ip.RowPixelPattern[x]] = (byte)_bitStream.ReadBit();
        //                }
        //            }
        //        }
        //        else // Non-interlaced
        //        {
        //            for (int y = 0; y < el.Height; y++, pos += el.Width)
        //                for (int x = 0; x < el.Width; x++)
        //                    for (int curPlane = plane; curPlane < plane + ip.ColorDepth; curPlane++)
        //                        ElementData[Format.MergePlanePriority[curPlane]][pos + ip.RowPixelPattern[x]] = (byte)_bitStream.ReadBit();
        //        }

        //        plane += ip.ColorDepth;
        //    }

        //    // Merge into foreign pixel data
        //    byte foreignPixelData;

        //    for (pos = 0; pos < MergedData.Length; pos++)
        //    {
        //        foreignPixelData = 0;
        //        for (int i = 0; i < Format.ColorDepth; i++)
        //            foreignPixelData |= (byte)(ElementData[i][pos] << i); // Works for SNES image data and palettes, may need customization later
        //        MergedData[pos] = foreignPixelData;
        //    }

        //    pos = 0;
        //    for (int y = 0; y < Height; y++)
        //        for (int x = 0; x < Width; x++, pos++)
        //            imageBuffer[x, y] = MergedData[pos];
        //}

        public ReadOnlySpan<byte> EncodeElement(ArrangerElement el, byte[,] imageBuffer)
        {
            if (imageBuffer.GetLength(0) != Width || imageBuffer.GetLength(1) != Height)
                throw new ArgumentException(nameof(imageBuffer));

            int pos = 0;
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++, pos++)
                    MergedData[pos] = imageBuffer[x, y];

            // Loop over MergedData to split foreign colors into bit planes in ElementData
            for (pos = 0; pos < MergedData.Length; pos++)
            {
                for (int i = 0; i < Format.ColorDepth; i++)
                    ElementData[i][pos] = (byte)((MergedData[pos] >> i) & 0x1);
            }

            // Loop over planes and write bits to data buffer with proper interlacing
            var bs = BitStream.OpenWrite(StorageSize, 8);
            int plane = 0;

            foreach (ImageProperty ip in Format.ImageProperties)
            {
                pos = 0;

                if (ip.RowInterlace)
                {
                    for (int y = 0; y < Format.Height; y++)
                    {
                        for (int curPlane = plane; curPlane < plane + ip.ColorDepth; curPlane++)
                        {
                            pos = y * el.Height;
                            for (int x = 0; x < Format.Width; x++)
                            {
                                int priorityPos = pos + ip.RowPixelPattern[x];
                                bs.WriteBit(ElementData[curPlane][priorityPos]);
                            }
                        }
                    }
                }
                else
                {
                    for (int y = 0; y < Format.Height; y++, pos += Format.Width)
                    {
                        for (int x = 0; x < Format.Width; x++)
                            for (int curPlane = plane; curPlane < plane + ip.ColorDepth; curPlane++)
                            {
                                bs.WriteBit(ElementData[curPlane][pos + ip.RowPixelPattern[x]]);
                            }
                    }
                }

                plane += ip.ColorDepth;
            }

            return bs.Data;
        }

        /// <summary>
        /// Encoding routine to encode indexed (palette-based) graphics
        /// </summary>
        /// <param name="el">Element to encode</param>
        /// <param name="imageBuffer">Contains indexed pixel data to encode</param>
        //public unsafe void Encode(ArrangerElement el, byte[,] imageBuffer)
        //{
        //    int pos = 0;
        //    for (int y = 0; y < Height; y++)
        //        for (int x = 0; x < Width; x++, pos++)
        //            MergedData[pos] = imageBuffer[x, y];

        //    // Loop over MergedData to split foreign colors into bit planes in ElementData
        //    for (pos = 0; pos < MergedData.Length; pos++)
        //    {
        //        for (int i = 0; i < Format.ColorDepth; i++)
        //            ElementData[i][pos] = (byte)((MergedData[pos] >> i) & 0x1);
        //    }

        //    // Loop over planes and write bits to data buffer with proper interlacing
        //    BitStream bs = BitStream.OpenWrite(Format.StorageSize, 8);
        //    int plane = 0;

        //    foreach (ImageProperty ip in Format.ImageProperties)
        //    {
        //        pos = 0;

        //        if (ip.RowInterlace)
        //        {
        //            for (int y = 0; y < Format.Height; y++)
        //            {
        //                for (int curPlane = plane; curPlane < plane + ip.ColorDepth; curPlane++)
        //                {
        //                    pos = y * el.Height;
        //                    for (int x = 0; x < Format.Width; x++, pos++)
        //                    {
        //                        int priorityPos = pos + ip.RowPixelPattern[x];
        //                        bs.WriteBit(ElementData[curPlane][priorityPos]);
        //                    }
        //                }
        //            }
        //        }
        //        else
        //        {
        //            for (int y = 0; y < Format.Height; y++, pos += Format.Width)
        //            {
        //                for (int x = 0; x < Format.Width; x++)
        //                    for (int curPlane = plane; curPlane < plane + ip.ColorDepth; curPlane++)
        //                    {
        //                        bs.WriteBit(ElementData[curPlane][pos + ip.RowPixelPattern[x]]);
        //                    }
        //            }
        //        }

        //        plane += ip.ColorDepth;
        //    }

        //    el.DataFile.Stream.Seek(el.FileAddress.FileOffset, SeekOrigin.Begin);
        //    BinaryWriter bw = new BinaryWriter(el.DataFile.Stream);
        //    bw.Write(bs.Data, 0, bs.Data.Length); // TODO: Fix with a shifted, merged write
        //}

        /// <summary>
        /// Reads a contiguous block of encoded pixel data
        /// </summary>
        public virtual ReadOnlySpan<byte> ReadElement(ArrangerElement el)
        {
            var buffer = new byte[(StorageSize + 7) / 8];
            var bitStream = BitStream.OpenRead(buffer, StorageSize);

            var fs = el.DataFile.Stream;

            // TODO: Add bit granularity to seek and read
            if (el.FileAddress + StorageSize > fs.Length * 8)
                return null;

            bitStream.SeekAbsolute(0);
            fs.ReadShifted(el.FileAddress, StorageSize, buffer);

            return buffer;
        }

        /// <summary>
        /// Writes a contiguous block of encoded pixel data
        /// </summary>
        public virtual void WriteElement(ArrangerElement el, ReadOnlySpan<byte> encodedBuffer)
        {
            // TODO: Add bit granularity to seek and read
            var fs = el.DataFile.Stream;
            fs.Seek(el.FileAddress.FileOffset, SeekOrigin.Begin);
            fs.Write(encodedBuffer);
        }
    }
}
