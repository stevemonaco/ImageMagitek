namespace ImageMagitek.Tables;

/// <summary>
/// Maps a game's pointers to file offsets and back. The pointer value 0 is null for every mapper.
/// </summary>
public interface IAddressMapper
{
    string Name { get; }

    /// <summary>
    /// Maps a non-null pointer to a file offset
    /// </summary>
    /// <returns>False when the pointer is null or unmapped</returns>
    bool TryMap(long pointer, out long offset);

    long ToPointer(long offset);

    /// <summary>
    /// The mapper to use over data of <paramref name="length"/> bytes, for mappers that detect a copier header from the length
    /// </summary>
    IAddressMapper ForDataLength(long length) => this;
}

/// <summary>
/// GBA cartridge ROM: <c>0x08000000</c>–<c>0x09FFFFFF</c> maps to the file from offset 0
/// </summary>
public sealed class GbaAddressMapper : IAddressMapper
{
    public const long RomBase = 0x08000000;
    public const long RomEnd = 0x0A000000;

    public string Name => "gba";

    public bool TryMap(long pointer, out long offset)
    {
        offset = pointer - RomBase;
        return pointer is >= RomBase and < RomEnd;
    }

    public long ToPointer(long offset) => offset + RomBase;
}

/// <summary>
/// A pointer is the file offset plus a fixed base
/// </summary>
public sealed class LinearAddressMapper(long baseAddress) : IAddressMapper
{
    public long Base { get; } = baseAddress;

    public string Name => "linear";

    public bool TryMap(long pointer, out long offset)
    {
        offset = pointer - Base;
        return pointer != 0 && pointer >= Base;
    }

    public long ToPointer(long offset) => offset + Base;
}

/// <summary>
/// SNES LoROM: bank <c>b</c> address <c>0x8000</c>–<c>0xFFFF</c> maps to <c>b * 0x8000 + (address - 0x8000)</c> plus the copier header,
/// with banks <c>0x80</c> and up mirroring banks <c>0x00</c> and up (LIB-TABLES-015, -016)
/// </summary>
/// <param name="header">Copier header size in bytes, or null to detect it from the data length</param>
public sealed class SnesLoRomAddressMapper(long? header) : IAddressMapper
{
    public const int BankSize = 0x8000;
    public const int CopierHeaderSize = 512;

    public long? Header { get; } = header;

    public string Name => "snes-lorom";

    public bool TryMap(long pointer, out long offset)
    {
        offset = 0;
        var bank = (pointer >> 16) & 0xFF;
        var address = pointer & 0xFFFF;
        if (pointer is <= 0 or > 0xFFFFFF || bank is 0x7E or 0x7F || address < BankSize)
            return false;

        offset = (bank & 0x7F) * BankSize + (address - BankSize) + (Header ?? 0);
        return true;
    }

    public long ToPointer(long offset)
    {
        var relative = offset - (Header ?? 0);
        var bank = relative / BankSize;
        return ((bank < 0x7E ? bank : bank | 0x80) << 16) | (BankSize + relative % BankSize);
    }

    public IAddressMapper ForDataLength(long length) =>
        Header is null ? new SnesLoRomAddressMapper(length % BankSize == CopierHeaderSize ? CopierHeaderSize : 0) : this;
}

public static class AddressMappers
{
    /// <summary>
    /// Creates the mapper named by a definition's <c>addressspace</c> element
    /// </summary>
    /// <param name="header">The copier header size, null to detect it; only SNES mappers take one</param>
    public static MagitekResult<IAddressMapper> Create(string name, long baseAddress, long? header = null)
    {
        if (header is not null && name is not ("snes-lorom" or "snes-hirom"))
            return new MagitekResult<IAddressMapper>.Failed($"Address space mapper '{name}' does not take a copier header");

        if (header is < 0 or >= SnesLoRomAddressMapper.BankSize)
            return new MagitekResult<IAddressMapper>.Failed($"Copier header {header} is outside 0 to 0x7FFF");

        return name switch
        {
            "gba" => new MagitekResult<IAddressMapper>.Success(new GbaAddressMapper()),
            "linear" => new MagitekResult<IAddressMapper>.Success(new LinearAddressMapper(baseAddress)),
            "snes-lorom" => new MagitekResult<IAddressMapper>.Success(new SnesLoRomAddressMapper(header)),
            "snes-hirom" or "nes" or "gb" =>
                new MagitekResult<IAddressMapper>.Failed($"Address space mapper '{name}' is not implemented yet"),
            _ => new MagitekResult<IAddressMapper>.Failed($"Unknown address space mapper '{name}'")
        };
    }
}
