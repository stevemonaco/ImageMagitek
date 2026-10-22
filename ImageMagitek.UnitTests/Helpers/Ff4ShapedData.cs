using System;
using System.Buffers.Binary;
using System.Linq;
using ImageMagitek.Tables;

namespace ImageMagitek.UnitTests;

/// <summary>
/// Synthetic LoROM bytes shaped like FF4's monster tables for <c>Fixtures/Tables/Ff4Synthetic.table.xml</c>: two regular monsters sharing
/// graphics and a palette, a 3bpp and a 4bpp boss placed by cell streams in bank $01, a skipped dummy and a third regular monster
/// </summary>
public static class Ff4ShapedData
{
    public const long Monsters = 0x100;
    private const long SizeClasses = 0x200;
    private const long Bosses = 0x300;
    public const long BossMaps = 0x400;
    private const long Names = 0x500;
    public const long MonsterTiles = 0x1000;
    public const long Boss4bppTiles = 0x2000;
    public const long BossTiles = 0x3000;
    public const long Palettes = 0x6000;
    private const long BossStream = 0x8100;
    private const long Boss4bppStream = 0x8110;
    private const int Size = 0x10000;

    /// <param name="header">Prepends a 512-byte copier header</param>
    public static byte[] Create(bool header = false)
    {
        var rom = new byte[Size];
        void Put16(long offset, int value) => BinaryPrimitives.WriteUInt16LittleEndian(rom.AsSpan((int)offset), (ushort)value);

        (byte Size, byte Palette, int Pointer)[] monsters =
        [
            (0, 1, 0x8200),
            (1, 1, 0x8200),
            (0x80, 0, 0x8600),
            (0x81, 0, 0x0400),
            (0, 5, 0x8200),
            (2, 3, 0x8200)
        ];

        for (var i = 0; i < monsters.Length; i++)
        {
            var offset = Monsters + 4 * i;
            rom[offset] = monsters[i].Size;
            rom[offset + 1] = monsters[i].Palette;
            Put16(offset + 2, monsters[i].Pointer);
        }

        byte[] sizeClasses = [2, 2, 3, 2, 2, 3];
        sizeClasses.CopyTo(rom, SizeClasses);

        // Boss 0: size class 1 with bit 0x40 set, palette 2, tilemap 0; boss 1: size class 2, palette 2, tilemap 1
        byte[] bosses = [0x00, 0x41, 2, 0, 0, 0x00, 0x02, 2, 0, 1];
        bosses.CopyTo(rom, Bosses);
        Put16(BossMaps, (int)(BossStream & 0xFFFF));
        Put16(BossMaps + 2, (int)(Boss4bppStream & 0xFFFF));

        // FF is one empty cell, FE n is n empty cells; bytes past the cell count (07, and the run of 9 cut to 5) are dropped
        byte[] bossStream = [0x00, 0xFF, 0x01, 0xFE, 0x02, 0x03, 0x07];
        byte[] boss4bppStream = [0x02, 0xFE, 0x09];
        bossStream.CopyTo(rom, BossStream);
        boss4bppStream.CopyTo(rom, Boss4bppStream);

        string[] names = ["Imp", "Bomb.", "Golbez", "Zeromus", "Dummy", "Mt.Dr.Ab"];
        for (var i = 0; i < names.Length; i++)
            Encode(names[i]).CopyTo(rom, Names + 8 * i);

        for (var i = 0; i < 0x200; i++)
            rom[MonsterTiles + i] = (byte)i;

        for (var i = 0; i < 6 * 16; i++)
            Put16(Palettes + 2 * i, 0x100 + i);

        return header ? new byte[SnesLoRomAddressMapper.CopierHeaderSize].Concat(rom).ToArray() : rom;
    }

    public static void SetMonsterPalette(byte[] rom, int monster, byte palette) => rom[Monsters + 4 * monster + 1] = palette;

    public static void SetBossPalette(byte[] rom, int boss, byte palette) => rom[Bosses + 5 * boss + 2] = palette;

    private static byte[] Encode(string text)
    {
        var bytes = Enumerable.Repeat((byte)0xFF, 8).ToArray();
        for (var i = 0; i < text.Length; i++)
        {
            bytes[i] = text[i] switch
            {
                '.' => 0xC1,
                >= 'a' and <= 'z' => (byte)(0x5C + text[i] - 'a'),
                _ => (byte)(0x42 + text[i] - 'A')
            };
        }

        return bytes;
    }
}
