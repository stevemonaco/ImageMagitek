using System;
using System.Buffers.Binary;
using System.Collections.Generic;

namespace ImageMagitek.Colors.Serialization;

public interface IColorSourceSerializer
{
    IColor[] LoadColors(IColorSource[] sources, DataFile df, ColorModel colorModel, int entries);
    void StoreColors(IList<IColorSource> sources, DataFile df, IList<ColorRgba32> nativeColors, IList<IColor> foreignColors);
}

public class ColorSourceSerializer : IColorSourceSerializer
{
    private readonly IColorFactory _colorFactory;

    public ColorSourceSerializer(IColorFactory colorFactory)
    {
        _colorFactory = colorFactory;
    }

    public IColor[] LoadColors(IColorSource[] sources, DataFile df, ColorModel colorModel, int entries)
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
                result[currentEntry] = ReadFileColor(df, fileSource.Offset, colorModel, size, fileSource.Endian);
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

    /// <summary>
    /// 
    /// </summary>
    /// <param name="df">Source to read color from</param>
    /// <param name="offset">Offset into the source to read from</param>
    /// <param name="colorModel">Model to create the color with</param>
    /// <param name="size">Read size in bits</param>
    /// <param name="endian">Endianness of the color</param>
    /// <returns>The ColorModel-mapped color</returns>
    /// <exception cref="NotSupportedException">Size must be 32 bits or less</exception>
    private IColor ReadFileColor(DataFile df, FileBitAddress offset, ColorModel colorModel, int size, Endian endian)
    {
        Span<byte> colorBuffer = stackalloc byte[4];

        int readSize = (size + 7) / 8;

        df.ReadUnshifted(offset, size, colorBuffer);

        uint readColor;
        if (readSize == 1)
        {
            readColor = colorBuffer[0];
        }
        else if (readSize == 2 && endian == Endian.Little)
        {
            readColor = BinaryPrimitives.ReadUInt16LittleEndian(colorBuffer);
        }
        else if (readSize == 2 && endian == Endian.Big)
        {
            readColor = BinaryPrimitives.ReadUInt16BigEndian(colorBuffer);
        }
        else if (readSize == 3)
        {
            readColor = colorBuffer[0];
            readColor |= ((uint)colorBuffer[1]) << 8;
            readColor |= ((uint)colorBuffer[2]) << 16;
        }
        else if (readSize == 4 && endian == Endian.Little)
        {
            readColor = BinaryPrimitives.ReadUInt32LittleEndian(colorBuffer);
        }
        else if (readSize == 4 && endian == Endian.Big)
        {
            readColor = BinaryPrimitives.ReadUInt32BigEndian(colorBuffer);
        }
        else
        {
            throw new NotSupportedException($"{nameof(LoadColors)}: Palette formats with entry sizes larger than 4 bytes are not supported");
        }

        return _colorFactory.CreateColor(colorModel, readColor);
    }

    /// <summary>
    /// Stores color sources with FileColorSources being written to file and native/foreign sources being updated, but the caller must serialize the 
    /// project resource themself to update project sources on disk
    /// </summary>
    /// <param name="sources"></param>
    /// <param name="df"></param>
    /// <param name="nativeColors"></param>
    /// <param name="foreignColors"></param>
    /// <exception cref="NotSupportedException"></exception>
    public void StoreColors(IList<IColorSource> sources, DataFile df, IList<ColorRgba32> nativeColors, IList<IColor> foreignColors)
    {
        for (int i = 0; i < sources.Count; i++)
        {
            if (sources[i] is FileColorSource fileSource)
            {
                WriteFileColor(df, fileSource.Offset, foreignColors[i], fileSource.Endian);
            }
            else if (sources[i] is ProjectNativeColorSource nativeSource)
            {
                nativeSource.Value = nativeColors[i];
            }
            else if (sources[i] is ProjectForeignColorSource foreignSource)
            {
                foreignSource.Value = foreignColors[i];
            }
            else if (sources[i] is ScatteredColorSource scatteredSource)
            {
                throw new NotSupportedException();
            }
        }
    }

    private void WriteFileColor(DataFile df, FileBitAddress offset, IColor foreignColor, Endian endian)
    {
        Span<byte> colorBuffer = stackalloc byte[4];

        int writeSize = (foreignColor.Size + 7) / 8;

        if (writeSize == 1)
        {
            colorBuffer[0] = (byte)foreignColor.Color;
        }
        else if (writeSize == 2 && endian == Endian.Little)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(colorBuffer, (ushort)foreignColor.Color);
        }
        else if (writeSize == 2 && endian == Endian.Big)
        {
            BinaryPrimitives.WriteUInt16BigEndian(colorBuffer, (ushort)foreignColor.Color);
        }
        else if (writeSize == 3)
        {
            colorBuffer[0] = (byte)foreignColor.Color;
            colorBuffer[1] = (byte)(foreignColor.Color >> 8);
            colorBuffer[2] = (byte)(foreignColor.Color >> 16);
        }
        else if (writeSize == 4 && endian == Endian.Little)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(colorBuffer, foreignColor.Color);
        }
        else if (writeSize == 4 && endian == Endian.Big)
        {
            BinaryPrimitives.WriteUInt32BigEndian(colorBuffer, foreignColor.Color);
        }

        df.Write(offset.FileOffset, colorBuffer[..writeSize]);
    }
}
