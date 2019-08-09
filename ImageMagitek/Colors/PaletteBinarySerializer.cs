using ImageMagitek.ExtensionMethods;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ImageMagitek.Colors
{
    public static class PaletteBinarySerializer
    {
        public static IColor32[] ReadPalette(DataFile df, FileBitAddress address, ColorModel colorModel, int entries)
        {
            var color = ColorFactory.CreateColor(colorModel);
            int readSize = (color.Size + 7) / 8;

            byte[] paletteData = df.Stream.ReadUnshifted(address, readSize * 8 * entries, true);
            BitStream bs = BitStream.OpenRead(paletteData, readSize * 8 * entries);
            var colors = new IColor32[entries];

            for (int i = 0; i < entries; i++)
            {
                uint readColor;

                if (readSize == 1)
                    readColor = bs.ReadByte();
                else if (readSize == 2)
                {
                    readColor = bs.ReadByte();
                    readColor |= ((uint)bs.ReadByte()) << 8;
                }
                else if (readSize == 3)
                {
                    readColor = bs.ReadByte();
                    readColor |= ((uint)bs.ReadByte()) << 8;
                    readColor |= ((uint)bs.ReadByte()) << 16;
                }
                else if (readSize == 4)
                {
                    readColor = bs.ReadByte();
                    readColor |= ((uint)bs.ReadByte()) << 8;
                    readColor |= ((uint)bs.ReadByte()) << 16;
                    readColor |= ((uint)bs.ReadByte()) << 24;
                }
                else
                    throw new NotSupportedException($"{nameof(ReadPalette)}: Palette formats with entry sizes larger than 4 bytes are not supported");

                color = ColorFactory.CreateColor(colorModel, readColor);
                colors[i] = color;
            }

            return colors;
        }

        public static void WritePalette(DataFile df, FileBitAddress address, IEnumerable<IColor32> colors)
        {
            int writeSize = (colors.First().Size + 7) / 8;

            df.Stream.Seek(address.FileOffset, SeekOrigin.Begin);
            using var bw = new BinaryWriter(df.Stream, Encoding.Default, true);

            foreach(var color in colors)
            {
                if (writeSize == 1)
                    bw.Write((byte)color.Color);
                else if (writeSize == 2)
                {
                    bw.Write((byte)color.Color);
                    bw.Write((byte)(color.Color >> 8));
                }
                else if (writeSize == 3)
                {
                    bw.Write((byte)color.Color);
                    bw.Write((byte)(color.Color >> 8));
                    bw.Write((byte)(color.Color >> 16));
                }
                else if (writeSize == 4)
                {
                    bw.Write((byte)color.Color);
                    bw.Write((byte)(color.Color >> 8));
                    bw.Write((byte)(color.Color >> 16));
                    bw.Write((byte)(color.Color >> 24));
                }
            }
        }
    }
}
