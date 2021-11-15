using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FF5MonsterSprites.Models;
using ImageMagitek;
using ImageMagitek.Builders;
using ImageMagitek.Codec;
using ImageMagitek.Colors;
using ImageMagitek.PluginSample;

namespace FF5MonsterSprites.Serialization;

public record SpriteResourceContext(DataFile DataFile, Palette Palette, ScatteredArranger Arranger);

public class MonsterSerializer
{
    public int MasterTableOffset { get; set; } = 0x14B180;
    public int TileSetOffset { get; set; } = 0x150000;
    public int PaletteOffset { get; set; } = 0x0ED000;
    public int FormSmallOffset { get; set; } = 0x10D004;
    public int FormLargeOffset { get; set; } = 0x10D334;
    public int Entries { get; set; } = 384;

    private int _monsterLength = 5;

    public async Task<List<MonsterMetadata>> DeserializeMonsters(string fileName)
    {
        using var fileStream = File.OpenRead(fileName);
        using var reader = new BinaryReader(fileStream);

        fileStream.Seek(MasterTableOffset, SeekOrigin.Begin);

        var monsterData = new byte[_monsterLength * Entries];
        var length = await fileStream.ReadAsync(monsterData, 0, monsterData.Length);
        var bitStream = BitStream.OpenRead(monsterData, monsterData.Length * 8);
        var monsters = new List<MonsterMetadata>();

        for (int i = 0; i < Entries; i++)
        {
            monsters.Add(DeserializeMonster(bitStream));
        }

        return monsters;
        
        MonsterMetadata DeserializeMonster(BitStream stream)
        {
            var depth = stream.ReadBit() == 1 ? TileColorDepth.Bpp3 : TileColorDepth.Bpp4;
            var tileSetID = stream.ReadBits(15);
            var size = stream.ReadBit() == 1 ? TileSetSize.Large : TileSetSize.Small;
            bool hasShadow = stream.ReadBit() == 0;
            int unused = stream.ReadBits(4);
            var paletteID = stream.ReadBits(10);
            var formID = stream.ReadBits(8);

            return new MonsterMetadata(depth, tileSetID, size, hasShadow, paletteID, formID, unused);
        }
    }

    public async Task<SpriteResourceContext> DeserializeSprite(string fileName, MonsterMetadata metadata)
    {
        var dataFile = new DataFile("monsterFile", fileName);
        var palEntries = metadata.ColorDepth == TileColorDepth.Bpp4 ? 16 : 8;

        var paletteSources = Enumerable.Range(0, palEntries)
            .Select(x => (IColorSource) new FileColorSource(8 * (PaletteOffset + 16 * metadata.PaletteID + x * 2), Endian.Little))
            .ToList();

        var pal = new Palette("monsterPalette", new ColorFactory(), ColorModel.Bgr15, paletteSources, true, PaletteStorageSource.Project);
        pal.DataFile = dataFile;

        int arrangerWidth = metadata.TileSetSize == TileSetSize.Small ? 8 : 16;
        int arrangerHeight = metadata.TileSetSize == TileSetSize.Small ? 8 : 16;

        var formData = new byte[arrangerWidth * arrangerHeight / 8];
        int formAddress = metadata.TileSetSize == TileSetSize.Small ? FormSmallOffset + 8 * metadata.FormID : FormLargeOffset + 32 * metadata.FormID;
        dataFile.Stream.Seek(formAddress, SeekOrigin.Begin);
        var length = await dataFile.Stream.ReadAsync(formData, 0, formData.Length);
        if (metadata.TileSetSize == TileSetSize.Large) // Requires endian swapping the tile form
        {
            EndianSwapArray(formData);
        }
        var bitStream = BitStream.OpenRead(formData, formData.Length * 8);

        var arranger = ArrangerBuilder.WithTiledLayout()
            .WithArrangerElementSize(arrangerWidth, arrangerHeight)
            .WithElementPixelSize(8, 8)
            .WithPixelColorType(PixelColorType.Indexed)
            .WithName("monsterArranger")
            .AsScatteredArranger()
            .Build();

        int elementsStored = 0;
        int tileAddress = TileSetOffset + 8 * metadata.TileSetID;
        int tileSize = metadata.ColorDepth == TileColorDepth.Bpp4 ? 32 : 24;

        for (int y = 0; y < arrangerHeight; y++)
        {
            for (int x = 0; x < arrangerWidth; x++)
            {
                if (bitStream.ReadBit() == 1)
                {
                    IGraphicsCodec codec = metadata.ColorDepth == TileColorDepth.Bpp4 ? new Snes4bppCodec(8, 8) : new Snes3bppCodec(8, 8);
                    var element = new ArrangerElement(x * 8, y * 8, dataFile, tileAddress * 8, codec, pal);
                    tileAddress += tileSize;
                    arranger.SetElement(element, x, y);
                    elementsStored++;
                }
            }
        }

        return new SpriteResourceContext(dataFile, pal, arranger);
    }

    private void EndianSwapArray(byte[] array)
    {
        for (int i = 0; i < array.Length; i+=2)
        {
            var temp = array[i];
            array[i] = array[i + 1];
            array[i + 1] = temp;
        }
    }
}
