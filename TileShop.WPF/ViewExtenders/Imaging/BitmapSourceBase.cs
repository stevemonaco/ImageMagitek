using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace TileShop.WPF.Imaging
{
    public abstract class BitmapSourceBase : BitmapSource
    {
        public override PixelFormat Format => PixelFormats.Bgra32;
        public override double DpiX => 96;
        public override double DpiY => 96;
        public override bool IsDownloading => false;

        protected abstract void CopyPixelsCore(Int32Rect sourceRect, int stride, int bufferSize, IntPtr buffer);

        public override void CopyPixels(Array pixels, int stride, int offset)
        {
            Int32Rect sourceRect = new Int32Rect(0, 0, PixelWidth, PixelHeight);
            base.CopyPixels(sourceRect, pixels, stride, offset);
        }

        public override void CopyPixels(Int32Rect sourceRect, Array pixels, int stride, int offset)
        {
            this.ValidateArrayAndGetInfo(pixels, out var elementSize, out var bufferSize, out var elementType);

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

        protected void ValidateArrayAndGetInfo(Array pixels, out int elementSize, out int sourceBufferSize, out Type elementType)
        {
            if (pixels is null)
                throw new ArgumentNullException(nameof(pixels));

            if (pixels.Rank == 1)
            {
                if (pixels.GetLength(0) <= 0)
                {
                    throw new ArgumentException(nameof(pixels));
                }
                else
                {
                    checked
                    {
                        object exemplar = pixels.GetValue(0);
                        elementSize = Marshal.SizeOf(exemplar);
                        sourceBufferSize = pixels.GetLength(0) * elementSize;
                        elementType = exemplar.GetType();
                    }
                }
            }
            else if (pixels.Rank == 2)
            {
                if (pixels.GetLength(0) <= 0 || pixels.GetLength(1) <= 0)
                {
                    throw new ArgumentException(nameof(pixels));
                }
                else
                {
                    checked
                    {
                        object exemplar = pixels.GetValue(0, 0);
                        elementSize = Marshal.SizeOf(exemplar);
                        sourceBufferSize = pixels.GetLength(0) * pixels.GetLength(1) * elementSize;
                        elementType = exemplar.GetType();
                    }
                }
            }
            else
            {
                throw new ArgumentException(nameof(pixels));
            }
        }

#pragma warning disable CS0067
        // Unused events that are required to be present, else System.NotImplementedException will be thrown by WPF/WIC
        public override event EventHandler<ExceptionEventArgs> DecodeFailed;
        public override event EventHandler DownloadCompleted;
        public override event EventHandler<DownloadProgressEventArgs> DownloadProgress;
        public override event EventHandler<ExceptionEventArgs> DownloadFailed;
#pragma warning restore CS0067
    }
}
