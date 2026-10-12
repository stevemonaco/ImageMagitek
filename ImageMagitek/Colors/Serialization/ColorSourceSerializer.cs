using System;
using System.Buffers.Binary;
using System.Collections.Generic;

namespace ImageMagitek.Colors.Serialization;

public interface IColorSourceSerializer
{
    IColor[] LoadColors(IColorSource[] sources, DataSource dataSource, ColorModel colorModel, int entries);
    void StoreColors(IList<IColorSource> sources, DataSource dataSource, IList<ColorRgba32> nativeColors, IList<IColor> foreignColors);
}

public class ColorSourceSerializer : IColorSourceSerializer
{
    private readonly IColorFactory _colorFactory;

    public ColorSourceSerializer(IColorFactory colorFactory)
    {
        _colorFactory = colorFactory;
    }

    public IColor[] LoadColors(IColorSource[] sources, DataSource dataSource, ColorModel colorModel, int entries)
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
                result[currentEntry] = ReadFileColor(dataSource, fileSource.Offset, colorModel, size, fileSource.Endian);
            }
            else if (source is ProjectNativeColorSource nativeSource)
            {
                result[currentEntry] = _colorFactory.ToForeign(nativeSource.Value, colorModel);
            }
            else if (source is ProjectForeignColorSource foreignSource)
            {
                result[currentEntry] = foreignSource.Value;
            }
            else if (source is ScatteredColorSource scatteredSource)
            {
                result[currentEntry] = _colorFactory.CreateColor(colorModel, ReadScatteredValue(dataSource, scatteredSource));
            }
            else
            {
                throw new NotSupportedException($"Color source of type '{source.GetType()}' is not supported");
            }

            currentEntry++;
        }

        return result;
    }

    private IColor ReadFileColor(DataSource dataSource, long offset, ColorModel colorModel, int size, Endian endian) =>
        _colorFactory.CreateColor(colorModel, ReadFileColorValue(dataSource, offset, size, endian));

    /// <summary>
    /// Reads the raw, uninterpreted color value stored at byte <paramref name="offset"/>
    /// </summary>
    /// <param name="size">Read size in bits</param>
    /// <exception cref="NotSupportedException">Size must be 32 bits or less</exception>
    public static uint ReadFileColorValue(DataSource dataSource, long offset, int size, Endian endian)
    {
        Span<byte> colorBuffer = stackalloc byte[4];

        int readSize = (size + 7) / 8;

        dataSource.Read(new BitAddress(offset, 0), size, colorBuffer);

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

        return readColor;
    }

    /// <summary>
    /// Assembles the raw, uninterpreted color value from the bytes that <paramref name="source"/>'s fields read
    /// </summary>
    public static uint ReadScatteredValue(DataSource dataSource, ScatteredColorSource source)
    {
        Span<byte> buffer = stackalloc byte[1];
        uint value = 0;

        foreach (var field in source.Fields)
        {
            dataSource.Read(new BitAddress(field.Offset, 0), 8, buffer);
            var mask = (1u << field.Bits) - 1;
            value |= ((uint)(buffer[0] >> field.LowBit) & mask) << field.Shift;
        }

        return value;
    }

    /// <summary>
    /// Splits <paramref name="value"/> into <paramref name="source"/>'s fields, changing only each field's bits within its byte
    /// </summary>
    private static void WriteScatteredValue(DataSource dataSource, ScatteredColorSource source, uint value)
    {
        Span<byte> buffer = stackalloc byte[1];

        foreach (var field in source.Fields)
        {
            var address = new BitAddress(field.Offset, 0);
            dataSource.Read(address, 8, buffer);

            var mask = ((1u << field.Bits) - 1) << field.LowBit;
            var bits = ((value >> field.Shift) << field.LowBit) & mask;
            buffer[0] = (byte)((buffer[0] & ~mask) | bits);

            dataSource.Write(address, buffer);
        }
    }

    /// <summary>
    /// Stores color sources with file and scattered sources being written to file and native/foreign sources being updated, but the caller must serialize the 
    /// project resource themself to update project sources on disk
    /// </summary>
    /// <param name="sources"></param>
    /// <param name="df"></param>
    /// <param name="nativeColors"></param>
    /// <param name="foreignColors"></param>
    /// <exception cref="NotSupportedException"></exception>
    public void StoreColors(IList<IColorSource> sources, DataSource df, IList<ColorRgba32> nativeColors, IList<IColor> foreignColors)
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
                WriteScatteredValue(df, scatteredSource, foreignColors[i].Color);
            }
            else
            {
                throw new NotSupportedException($"Color source of type '{sources[i].GetType()}' is not supported");
            }
        }
    }

    private void WriteFileColor(DataSource df, long offset, IColor foreignColor, Endian endian)
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

        df.Write(new BitAddress(offset, 0), colorBuffer[..writeSize]);
    }
}
