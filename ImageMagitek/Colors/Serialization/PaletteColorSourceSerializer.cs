using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ImageMagitek.ExtensionMethods;

namespace ImageMagitek.Colors.Serialization
{
    public interface IPaletteColorSourceSerializer
    {
        IColor[] ReadPalette(IColorSource[] sources, DataFile df, ColorModel colorModel, int entries);
        void WritePalette(IColorSource[] sources, DataFile df, IEnumerable<IColor> colors);
    }

    public class PaletteColorSourceSerializer : IPaletteColorSourceSerializer
    {
        private readonly IColorFactory _colorFactory;
        private readonly byte[] _colorBuffer = new byte[8];

        public PaletteColorSourceSerializer(IColorFactory colorFactory)
        {
            _colorFactory = colorFactory;
        }

        public IColor[] ReadPalette(IColorSource[] sources, DataFile df, ColorModel colorModel, int entries)
        {
            var result = new IColor[entries];
            var currentEntry = 0;
            var size = _colorFactory.CreateColor(colorModel).Size;

            foreach (var source in sources)
            {
                if (currentEntry == entries)
                    return result;

                if (source is FileColorSource fileSource)
                {
                    result[currentEntry] = ReadFileColor(df, fileSource.Offset, colorModel, size);
                }
                else if (source is ProjectNativeColorSource nativeSource)
                {
                    result[currentEntry] = nativeSource.Value;
                }
                else if (source is ProjectForeignColorSource foreignSource)
                {
                    result[currentEntry] = foreignSource.Value;
                }
                else if (source is ScatteredColorSource scatteredSource)
                {

                }

                currentEntry++;
            }

            return result;
        }

        private IColor ReadFileColor(DataFile df, FileBitAddress offset, ColorModel colorModel, int size)
        {
            int readSize = (size + 7) / 8;
            byte[] paletteData = df.Stream.ReadUnshifted(offset, readSize * 8);

            df.Stream.Seek(offset.FileOffset, SeekOrigin.Begin);
            df.Stream.ReadUnshifted(offset, size, _colorBuffer);
            //BitStream bs = BitStream.OpenRead(paletteData, readSize * 8);

            uint readColor;
            if (readSize == 1)
                readColor = (uint)_colorBuffer[0];
            else if (readSize == 2)
            {
                readColor = BinaryPrimitives.ReadUInt16LittleEndian(_colorBuffer);
            }
            else if (readSize == 3)
            {
                readColor = _colorBuffer[0];
                readColor |= ((uint)_colorBuffer[1]) << 8;
                readColor |= ((uint)_colorBuffer[2]) << 16;
            }
            else if (readSize == 4)
            {
                readColor = BinaryPrimitives.ReadUInt32LittleEndian(_colorBuffer);
            }
            else
                throw new NotSupportedException($"{nameof(ReadPalette)}: Palette formats with entry sizes larger than 4 bytes are not supported");

            return _colorFactory.CreateColor(colorModel, readColor);
        }

        public void WritePalette(IColorSource[] sources, DataFile df, IEnumerable<IColor> colors)
        {
            throw new NotImplementedException();
        }
    }
}
