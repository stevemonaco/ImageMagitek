using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Drawing;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Primitives;
using ImageMagitek.Codec;
using ImageMagitek.Project;
using System.Runtime.InteropServices;

namespace ImageMagitek
{
    /// <summary>
    /// GraphicsCodec provides a generalized method to encode/decode bitmap formats
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
    public class GraphicsCodec
    {
        #region Graphics Decoding Functions

        /// <summary>
        /// General-purpose routine to decode a single arranger element
        /// </summary>
        /// <param name="image">Image to draw onto</param>
        /// <param name="el">ArrangerElement to decode</param>
        public static void Decode(Image<Argb32> image, ArrangerElement el)
        {
            if (el.GraphicsFormat.ColorType == PixelColorType.Indexed)
                IndexedDecode(image, el);
            else if (el.GraphicsFormat.ColorType == PixelColorType.Direct)
                DirectDecode(image, el);
        }

        /// <summary>
        /// Decoding routine to decode indexed (palette-based) graphics
        /// </summary>
        /// <param name="image">Destination bitmap</param>
        /// <param name="el">Element to decode</param>
        unsafe static void IndexedDecode(Image<Argb32> image, ArrangerElement el)
        {
            FileStream fs = el.Parent.GetResourceRelative<DataFile>(el.DataFileKey).Stream;
            GraphicsFormat format = el.GraphicsFormat;

            format.Resize(el.Width, el.Height);

            if (el.FileAddress + el.StorageSize > fs.Length * 8) // Element would contain data past the end of the file
            {
                DecodeBlank(image, el);
                return;
            }

            byte[] Data = fs.ReadUnshifted(el.FileAddress, el.StorageSize, true);

            BitStream bs = BitStream.OpenRead(Data, el.StorageSize); // TODO: Change to account for first bit alignment

            int plane = 0;
            int pos = 0;

            // Deinterlace into separate bitplanes
            foreach (ImageProperty ip in format.ImageProperties)
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
                                el.ElementData[format.MergePriority[curPlane]][pos + ip.RowExtendedPixelPattern[x]] = (byte)bs.ReadBit();
                        }
                    }
                }
                else // Non-interlaced
                {
                    for (int y = 0; y < el.Height; y++, pos += el.Width)
                        for (int x = 0; x < el.Width; x++)
                            for (int curPlane = plane; curPlane < plane + ip.ColorDepth; curPlane++)
                                el.ElementData[format.MergePriority[curPlane]][pos + ip.RowExtendedPixelPattern[x]] = (byte)bs.ReadBit();
                }

                plane += ip.ColorDepth;
            }

            // Merge into foreign pixel data
            byte foreignPixelData = 0;

            for (pos = 0; pos < el.MergedData.Length; pos++)
            {
                foreignPixelData = 0;
                for (int i = 0; i < format.ColorDepth; i++)
                    foreignPixelData |= (byte)(el.ElementData[i][pos] << i); // Works for SNES palettes
                el.MergedData[pos] = foreignPixelData;
            }

            // Translate foreign colors to native colors and draw to bitmap
            DrawBitmapIndexedSafe(image, el);
        }

        public static void DirectDecode(Image<Argb32> image, ArrangerElement el)
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

        static void DrawBitmapIndexedSafe(Image<Argb32> image, ArrangerElement el)
        {
            var dest = image.GetPixelSpan();

            int destidx = image.Width * el.Y1 + el.X1;
            int srcidx = 0;
            var pal = el.Parent.GetResourceRelative<Palette>(el.PaletteKey);

            // Copy data into image
            for (int y = 0; y < el.Height; y++)
            {
                for(int x = 0; x < el.Width; x++, srcidx++, destidx++)
                {
                    var nc = pal[el.MergedData[srcidx]];
                    dest[destidx] = new Argb32(nc.R(), nc.G(), nc.B(), nc.A());
                }
                destidx += el.X1 + image.Width - (el.X2 + 1);
            }
        }

        /// <summary>
        /// Draws an indexed element onto an ARGB32 Bitmap at the specified location
        /// </summary>
        /// <param name="image">Destination bitmap</param>
        /// <param name="el">ArrangerElement to render</param>
        unsafe static void DrawBitmapIndexed(Image<Argb32> image, ArrangerElement el)
        {
            /*Rectangle lockRect = new Rectangle(0, 0, image.Width, image.Height);
            BitmapData bd = image.LockBits(lockRect, ImageLockMode.ReadOnly, image.PixelFormat);

            // Draw bitmap
            uint* dest = (uint*)bd.Scan0;
            int StrideWidth = bd.Stride - (image.Width * 4);

            dest += (bd.Stride / 4) * el.Y1; // Seek to the appropriate vertical scanline in the bitmap

            fixed (byte* fixedData = el.MergedData) // Fix el.MergedData in memory so unsafe pointers can be used
            {
                byte* src = fixedData;

                int Height = el.Y2 - el.Y1 + 1;
                int Width = el.X2 - el.X1 + 1;
                Palette pal = el.Parent.GetResourceRelative<Palette>(el.PaletteKey);

                for (int y = 0; y < Height; y++)
                {
                    dest += el.X1; // Seek to PixelX in the scanline
                    for (int x = 0; x < Width; x++)
                    {
                        *dest = pal[*src].Color;
                        dest++;
                        src++;
                    }
                    dest += (image.Width - el.X1 - Width);
                    dest += StrideWidth;
                }
            }

            image.UnlockBits(bd);*/
        }

        /// <summary>
        /// Draws a blank element using the 0-index color from the default palette
        /// Used for when an arranger does not have a graphic assigned to every element
        /// </summary>
        /// <param name="image">Bitmap to draw onto</param>
        /// <param name="el">Element with specified coordinates</param>
        public static void DecodeBlank(Image<Argb32> image, ArrangerElement el)
        {
            var dest = image.GetPixelSpan();

            int destidx = image.Width * el.Y1 + el.X1;
            var pal = el.Parent.GetResourceRelative<Palette>(el.PaletteKey);
            var nc = pal[0];
            var col = new Argb32(nc.R(), nc.G(), nc.B(), nc.A());

            // Copy data into image
            for (int y = 0; y < el.Height; y++)
            {
                for (int x = 0; x < el.Width; x++, destidx++)
                    dest[destidx] = col;
                destidx += el.X1 + image.Width - (el.X2 + 1);
            }
        }
        #endregion

        #region Graphics Encoding Functions
        public unsafe static void Encode(Image<Argb32> image, ArrangerElement el)
        {
            if (el.GraphicsFormat.ColorType == PixelColorType.Indexed)
                IndexedEncode(image, el);
            else if (el.GraphicsFormat.ColorType == PixelColorType.Direct)
                DirectEncode(image, el);
        }

        unsafe static void IndexedEncode(Image<Argb32> image, ArrangerElement el)
        {
            // ReadBitmap for local->foreign color conversion into fmt.MergedData
            ReadBitmapIndexed(image, el);

            FileStream fs = el.Parent.GetResourceRelative<DataFile>(el.DataFileKey).Stream;
            GraphicsFormat format = el.GraphicsFormat;

            // Loop over MergedData to split foreign colors into bit planes in fmt.TileData
            for (int pos = 0; pos < el.MergedData.Length; pos++)
            {
                for (int i = 0; i < format.ColorDepth; i++)
                    el.ElementData[i][pos] = (byte)((el.MergedData[pos] >> i) & 0x1);
            }

            // Loop over planes and putbit to data buffer with proper interlacing
            BitStream bs = BitStream.OpenWrite(el.StorageSize, 8);
            int plane = 0;

            foreach (ImageProperty ip in format.ImageProperties)
            {
                int pos = 0;

                if (ip.RowInterlace)
                {
                    for (int y = 0; y < el.Height; y++)
                    {
                        for (int curPlane = plane; curPlane < plane + ip.ColorDepth; curPlane++)
                        {
                            pos = y * el.Height;
                            //for (int x = 0; x < format.Width; x++, pos++)
                            //    bs.WriteBit(el.TileData[curPlane][pos]);
                            for (int x = 0; x < el.Width; x++)
                                bs.WriteBit(el.ElementData[format.MergePriority[curPlane]][pos + ip.RowPixelPattern[x]]);
                        }
                    }
                }
                else
                {
                    /*for (int y = 0; y < el.Height; y++, pos += el.Width)
                    {
                        for (int x = 0; x < el.Width; x++)
                            for (int curPlane = plane; curPlane < plane + ip.ColorDepth; curPlane++)
                                bs.WriteBit(el.ElementData[curPlane][pos + ip.RowPixelPattern[x]]);
                    }*/

                    for (int y = 0; y < el.Height; y++, pos += el.Width)
                    {
                        for (int x = 0; x < el.Width; x++)
                            for (int curPlane = plane; curPlane < plane + ip.ColorDepth; curPlane++)
                                bs.WriteBit(el.ElementData[format.MergePriority[curPlane]][pos + ip.RowPixelPattern[x]]);
                    }
                }

                plane += ip.ColorDepth;
            }

            BinaryWriter bw = new BinaryWriter(fs);
            bw.Write(bs.Data, 0, bs.Data.Length); // TODO: Fix with a shifted, merged write
        }

        unsafe static void DirectEncode(Image<Argb32> image, ArrangerElement el)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Reads an indexed element at a specified location on a ARGB32 Bitmap
        /// </summary>
        /// <param name="image">Source bitmap</param>
        /// <param name="el">Destination arranger</param>
        unsafe static void ReadBitmapIndexed(Image<Argb32> image, ArrangerElement el)
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
