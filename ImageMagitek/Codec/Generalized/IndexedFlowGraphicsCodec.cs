using System;
using System.Collections.Generic;
using System.Linq;
using ImageMagitek.ExtensionMethods;

namespace ImageMagitek.Codec
{
    public class IndexedFlowGraphicsCodec : IIndexedCodec
    {
        public string Name { get; set; }
        public FlowGraphicsFormat Format { get; private set; }
        public int StorageSize => Format.StorageSize;
        public ImageLayout Layout => Format.Layout;
        public PixelColorType ColorType => Format.ColorType;
        public int ColorDepth => Format.ColorDepth;
        public int Width => Format.Width;
        public int Height => Format.Height;

        public virtual ReadOnlySpan<byte> ForeignBuffer => _foreignBuffer;
        protected byte[] _foreignBuffer;

        protected byte[,] _nativeBuffer;
        public virtual byte[,] NativeBuffer => _nativeBuffer;

        public int DefaultWidth => Format.DefaultWidth;
        public int DefaultHeight => Format.DefaultHeight;
        public bool CanResize => !Format.FixedSize;
        public int WidthResizeIncrement { get; }
        public int HeightResizeIncrement => 1;

        /// <summary>
        /// Preallocated buffer that separates and stores pixel color data
        /// </summary>
        private List<byte[]> _elementData;

        /// <summary>
        /// Preallocated buffer that stores merged pixel color data
        /// </summary>
        private byte[] _mergedData;

        private BitStream _bitStream;

        public IndexedFlowGraphicsCodec(FlowGraphicsFormat format)
        {
            Format = format;
            Name = format.Name;
            AllocateBuffers();

            // Consider implementing resize increment with more accurate LCM approach
            // https://stackoverflow.com/questions/147515/least-common-multiple-for-3-or-more-numbers
            WidthResizeIncrement = format.ImageProperties.Max(x => x.RowPixelPattern.Count);
        }

        private void AllocateBuffers()
        {
            _elementData = new List<byte[]>();
            for (int i = 0; i < Format.ColorDepth; i++)
            {
                byte[] data = new byte[Format.Width * Format.Height];
                _elementData.Add(data);
            }

            _mergedData = new byte[Format.Width * Format.Height];

            _foreignBuffer = new byte[(StorageSize + 7) / 8];
            _nativeBuffer = new byte[Height, Width];

            _bitStream = BitStream.OpenRead(_foreignBuffer, StorageSize);
        }

        /// <inheritdoc/>
        public byte[,] DecodeElement(in ArrangerElement el, ReadOnlySpan<byte> encodedBuffer)
        {
            if (encodedBuffer.Length * 8 < StorageSize) // Decoding would require data past the end of the buffer
                throw new ArgumentException(nameof(encodedBuffer));

            encodedBuffer.Slice(0, _foreignBuffer.Length).CopyTo(_foreignBuffer);
            _bitStream.SeekAbsolute(0);

            int plane = 0;
            int scanlinePosition;

            // Deinterlace into separate bitplanes
            foreach (ImageProperty ip in Format.ImageProperties)
            {
                if (ip.RowInterlace)
                {
                    for (int y = 0; y < el.Height; y++)
                    {
                        for (int curPlane = plane; curPlane < plane + ip.ColorDepth; curPlane++)
                        {
                            scanlinePosition = y * el.Width;
                            for (int x = 0; x < el.Width; x++)
                            {
                                var mergePlane = Format.MergePlanePriority[curPlane];
                                var pixelPosition = scanlinePosition + ip.RowPixelPattern[x];
                                _elementData[mergePlane][pixelPosition] = (byte)_bitStream.ReadBit();
                            }
                        }
                    }
                }
                else // Non-interlaced
                {
                    for (int y = 0; y < el.Height; y++)
                    {
                        for (int x = 0; x < el.Width; x++)
                        {
                            scanlinePosition = y * el.Width;
                            for (int curPlane = plane; curPlane < plane + ip.ColorDepth; curPlane++)
                            {
                                var mergePlane = Format.MergePlanePriority[curPlane];
                                int pixelPosition = scanlinePosition + ip.RowPixelPattern[x];
                                _elementData[mergePlane][pixelPosition] = (byte)_bitStream.ReadBit();
                            }
                        }
                    }
                }

                plane += ip.ColorDepth;
            }

            // Merge into foreign pixel data 
            byte foreignPixelData;

            for (scanlinePosition = 0; scanlinePosition < _mergedData.Length; scanlinePosition++)
            {
                foreignPixelData = 0;
                for (int i = 0; i < Format.ColorDepth; i++)
                    foreignPixelData |= (byte)(_elementData[i][scanlinePosition] << i); // Works for SNES image data and palettes, may need customization later
                _mergedData[scanlinePosition] = foreignPixelData;
            }

            scanlinePosition = 0;
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++, scanlinePosition++)
                    _nativeBuffer[y, x] = _mergedData[scanlinePosition];

            return NativeBuffer;
        }

        /// <inheritdoc/>
        public ReadOnlySpan<byte> EncodeElement(in ArrangerElement el, byte[,] imageBuffer)
        {
            if (imageBuffer.GetLength(0) != Height || imageBuffer.GetLength(1) != Width)
                throw new ArgumentException(nameof(imageBuffer));

            int pos = 0;
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++, pos++)
                    _mergedData[pos] = imageBuffer[y, x];

            // Loop over MergedData to split foreign colors into bit planes in ElementData
            for (pos = 0; pos < _mergedData.Length; pos++)
            {
                for (int i = 0; i < Format.ColorDepth; i++)
                    _elementData[i][pos] = (byte)((_mergedData[pos] >> i) & 0x1);
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
                                int mergedPlane = Format.MergePlanePriority[curPlane];
                                bs.WriteBit(_elementData[mergedPlane][priorityPos]);
                            }
                        }
                    }
                }
                else
                {
                    for (int y = 0; y < Format.Height; y++, pos += Format.Width)
                    {
                        for (int x = 0; x < Format.Width; x++)
                        {
                            for (int curPlane = plane; curPlane < plane + ip.ColorDepth; curPlane++)
                            {
                                int priorityPos = pos + ip.RowPixelPattern[x];
                                int mergedPlane = Format.MergePlanePriority[curPlane];
                                bs.WriteBit(_elementData[mergedPlane][priorityPos]);
                            }
                        }
                    }
                }

                plane += ip.ColorDepth;
            }

            return bs.Data;
        }

        /// <summary>
        /// Reads a contiguous block of foreign pixel data
        /// </summary>
        public virtual ReadOnlySpan<byte> ReadElement(in ArrangerElement el)
        {
            var buffer = new byte[(StorageSize + 7) / 8];
            var fs = el.DataFile.Stream;

            if (el.FileAddress + StorageSize > fs.Length * 8)
                return null;

            fs.ReadShifted(el.FileAddress, StorageSize, buffer);

            return buffer;
        }

        /// <summary>
        /// Writes a contiguous block of foreign pixel data
        /// </summary>
        public virtual void WriteElement(in ArrangerElement el, ReadOnlySpan<byte> encodedBuffer)
        {
            var fs = el.DataFile.Stream;
            fs.WriteShifted(el.FileAddress, StorageSize, encodedBuffer);
        }

        public int GetPreferredWidth(int width)
        {
            if (!CanResize)
                return DefaultWidth;

            return Math.Clamp(width - width % WidthResizeIncrement, WidthResizeIncrement, int.MaxValue);
        }

        public int GetPreferredHeight(int height)
        {
            if (!CanResize)
                return DefaultHeight;

            return Math.Clamp(height - height % HeightResizeIncrement, HeightResizeIncrement, int.MaxValue);
        }
    }
}
