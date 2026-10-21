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

public static class AddressMappers
{
    /// <summary>
    /// Creates the mapper named by a definition's <c>addressspace</c> element
    /// </summary>
    public static MagitekResult<IAddressMapper> Create(string name, long baseAddress) => name switch
    {
        "gba" => new MagitekResult<IAddressMapper>.Success(new GbaAddressMapper()),
        "linear" => new MagitekResult<IAddressMapper>.Success(new LinearAddressMapper(baseAddress)),
        "snes-lorom" or "snes-hirom" or "nes" or "gb" =>
            new MagitekResult<IAddressMapper>.Failed($"Address space mapper '{name}' is not implemented yet"),
        _ => new MagitekResult<IAddressMapper>.Failed($"Unknown address space mapper '{name}'")
    };
}
