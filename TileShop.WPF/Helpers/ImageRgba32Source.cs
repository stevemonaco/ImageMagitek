using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Advanced;

namespace TileShop.WPF.Helpers
{
    /// <summary>
    /// Adapts ImageSharp Rgba32 to BitmapSource for WPF
    /// </summary>
    /// <remarks>
    /// Implementation based upon: https://github.com/jongleur1983/SharpImageSource/blob/master/ImageSharp.WpfImageSource/ImageSharpImageSource.cs
    /// Reference: http://www.i-programmer.info/programming/wpf-workings/822
    /// Reference: https://blogs.msdn.microsoft.com/dwayneneed/2008/06/20/implementing-a-custom-bitmapsource/
    /// </remarks>
    public class ImageRgba32Source : BitmapSource
    {
        private Image<Rgba32> _source;

        public ImageRgba32Source(Image<Rgba32> source)
        {
            _source = source;
        }

        public ImageRgba32Source(int width, int height)
        {
            _source = new Image<Rgba32>(width, height);
        }

        protected override Freezable CreateInstanceCore()
        {
            var source = new Image<Rgba32>(16, 16);
            return new ImageRgba32Source(source);
        }

        public override PixelFormat Format => PixelFormats.Bgra32;
        public override int PixelHeight => _source.Height;
        public override int PixelWidth => _source.Width;
        public override double DpiX => _source.MetaData.HorizontalResolution;
        public override double DpiY => _source.MetaData.VerticalResolution;
        public override BitmapPalette Palette => null;
        public override event EventHandler<ExceptionEventArgs> DecodeFailed;

        public override bool IsDownloading => false;
        public override event EventHandler DownloadCompleted;
        public override event EventHandler<DownloadProgressEventArgs> DownloadProgress;
        public override event EventHandler<ExceptionEventArgs> DownloadFailed;

        public override void CopyPixels(Array pixels, int stride, int offset)
        {
            Int32Rect sourceRect = new Int32Rect(0, 0, PixelWidth, PixelHeight);
            base.CopyPixels(sourceRect, pixels, stride, offset);
        }

        public override void CopyPixels(
            Int32Rect sourceRect,
            Array pixels,
            int stride,
            int offset)
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
            if (elementType == null || !elementType.IsValueType)
            {
                throw new ArgumentException("must be a valueType!", nameof(pixels));
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

        public override void CopyPixels(
            Int32Rect sourceRect,
            IntPtr buffer,
            int bufferSize,
            int stride)
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

        private void CopyPixelsCore(
            Int32Rect sourceRect,
            int stride,
            int bufferSize,
            IntPtr buffer)
        {
            if (_source is object)
            {
                unsafe
                {
                    byte* pBytes = (byte*)buffer.ToPointer();
                    for (int y = 0; y < sourceRect.Height; y++)
                    {
                        var row = _source.GetPixelRowSpan(y);

                        for (int x = 0; x < sourceRect.Width; x++)
                        {
                            pBytes[x*4] = row[x].B;
                            pBytes[x*4 + 1] = row[x].G;
                            pBytes[x*4 + 2] = row[x].R;
                            pBytes[x*4 + 3] = row[x].A;
                        }

                        pBytes += stride;
                    }
                }
            }
        }

        private void ValidateArrayAndGetInfo(
            Array pixels,
            out int elementSize,
            out int sourceBufferSize,
            out Type elementType)
        {
            if (pixels == null)
            {
                throw new ArgumentNullException(nameof(pixels));
            }

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
    }
}
