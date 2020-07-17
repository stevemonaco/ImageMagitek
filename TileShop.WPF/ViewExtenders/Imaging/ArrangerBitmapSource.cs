using System;
using System.Runtime.InteropServices;
using System.Windows;

namespace TileShop.WPF.Imaging
{
    /// <summary>
    /// Abstract base class for adapting images to BitmapSource for WPF
    /// </summary>
    /// <remarks>
    /// Implementation based upon: https://github.com/jongleur1983/SharpImageSource/blob/master/ImageSharp.WpfImageSource/ImageSharpImageSource.cs
    /// Reference: http://www.i-programmer.info/programming/wpf-workings/822
    /// Reference: https://blogs.msdn.microsoft.com/dwayneneed/2008/06/20/implementing-a-custom-bitmapsource/
    /// </remarks>
    public abstract class ArrangerBitmapSource : BitmapSourceBase
    {
        public int CropX { get; protected set; }
        public int CropY { get; protected set; }

        public override void CopyPixels(Array pixels, int stride, int offset)
        {
            Int32Rect sourceRect = new Int32Rect(0, 0, PixelWidth, PixelHeight);
            base.CopyPixels(sourceRect, pixels, stride, offset);
        }

        public override void CopyPixels(Int32Rect sourceRect, Array pixels, int stride, int offset)
        {
            this.ValidateArrayAndGetInfo(
                pixels,
                out var elementSize,
                out var bufferSize,
                out var elementType);

            if (offset < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(offset));
            }

            // We accept arrays of arbitrary value types - but not reference types.
            if (elementType is null || !elementType.IsValueType)
            {
                throw new ArgumentException("must be a ValueType", nameof(pixels));
            }

            checked
            {
                int offsetInBytes = offset * elementSize;
                if (offsetInBytes >= bufferSize)
                {
                    throw new IndexOutOfRangeException();
                }

                // Get the address of the data in the array by pinning it.
                GCHandle arrayHandle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
                try
                {
                    // Adjust the buffer and bufferSize to account for the offset.
                    IntPtr buffer = arrayHandle.AddrOfPinnedObject();
                    buffer = new IntPtr(((long)buffer) + (long)offsetInBytes);
                    bufferSize -= offsetInBytes;

                    CopyPixels(sourceRect, buffer, bufferSize, stride);
                }
                finally
                {
                    arrayHandle.Free();
                }
            }
        }

        public override void CopyPixels(Int32Rect sourceRect, IntPtr buffer, int bufferSize, int stride)
        {
            // WIC would specify NULL for the source rect to indicate that the
            // entire content should be copied.  WPF turns that into an empty
            // rect, which we inflate here to be the entire bounds.
            if (sourceRect.IsEmpty)
            {
                sourceRect.Width = PixelWidth;
                sourceRect.Height = PixelHeight;
            }

            if (sourceRect.X < 0
                || sourceRect.Width < 0
                || sourceRect.Y < 0
                || sourceRect.Height < 0
                || sourceRect.X + sourceRect.Width > PixelWidth
                || sourceRect.Y + sourceRect.Height > PixelHeight)
            {
                throw new ArgumentOutOfRangeException(nameof(sourceRect));
            }

            if (buffer == IntPtr.Zero)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            checked
            {
                if (stride < 1)
                {
                    throw new ArgumentOutOfRangeException(nameof(stride));
                }

                uint minStrideInBits = (uint)(sourceRect.Width * Format.BitsPerPixel);
                uint minStrideInBytes = ((minStrideInBits + 7) / 8);
                if (stride < minStrideInBytes)
                {
                    throw new ArgumentOutOfRangeException(nameof(stride));
                }

                if (bufferSize < 1)
                {
                    throw new ArgumentOutOfRangeException(nameof(bufferSize));
                }

                uint minBufferSize = (uint)((sourceRect.Height - 1) * stride) + minStrideInBytes;
                if (bufferSize < minBufferSize)
                {
                    throw new ArgumentOutOfRangeException(nameof(bufferSize));
                }
            }

            CopyPixelsCore(sourceRect, stride, bufferSize, buffer);
        }
    }
}
