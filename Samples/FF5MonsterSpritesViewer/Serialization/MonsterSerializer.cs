using FF5MonsterSprites.Models;
using ImageMagitek;
using ImageMagitek.Builders;
using ImageMagitek.Codec;
using ImageMagitek.Colors;
using ImageMagitek.PluginSamples;

namespace FF5MonsterSprites.Serialization;

public record SpriteResourceContext(DataSource DataFile, ScatteredArranger Arranger);

public class MonsterSerializer
{
    private const int MasterTableOffset = 0x14B180;
    private const int TileSetOffset = 0x150000;
    private const int PaletteOffset = 0x0ED000;
    private const int FormSmallOffset = 0x10D004;
    private const int FormLargeOffset = 0x10D334;
    private const int Entries = 384;

    private const int MonsterLength = 5;

    public async Task<List<MonsterMetadata>> DeserializeMonsters(string fileName)
    {
        using var fileStream = File.OpenRead(fileName);

        fileStream.Seek(MasterTableOffset, SeekOrigin.Begin);

        var monsterData = new byte[MonsterLength * Entries];
        await fileStream.ReadExactlyAsync(monsterData);
        var bitStream = BitStream.OpenRead(monsterData, monsterData.Length * 8);
        var monsters = new List<MonsterMetadata>();

        for (int i = 0; i < Entries; i++)
        {
            monsters.Add(DeserializeMonster(bitStream));
        }

        return monsters;
        
        MonsterMetadata DeserializeMonster(IBitStreamReader stream)
        {
            var depth = stream.ReadBit() == 1 ? TileColorDepth.Bpp3 : TileColorDepth.Bpp4;
            var tileSetId = stream.ReadBits(15);
            var size = stream.ReadBit() == 1 ? TileSetSize.Large : TileSetSize.Small;
            bool hasShadow = stream.ReadBit() == 0;
            int unused = stream.ReadBits(4);
            var paletteId = stream.ReadBits(10);
            var formId = stream.ReadBits(8);

            return new MonsterMetadata(depth, tileSetId, size, hasShadow, paletteId, formId, unused);
        }
    }

    public async Task<SpriteResourceContext> DeserializeSprite(string fileName, MonsterMetadata metadata)
    {
        var dataFile = new FileDataSource("monsterFile", fileName);
        try
        {
            return await BuildSprite(dataFile, metadata);
        }
        catch
        {
            dataFile.Dispose();
            throw;
        }
    }

    private async Task<SpriteResourceContext> BuildSprite(FileDataSource dataFile, MonsterMetadata metadata)
    {
        var palEntries = metadata.ColorDepth == TileColorDepth.Bpp4 ? 16 : 8;

        var paletteSources = Enumerable.Range(0, palEntries)
            .Select(x => (IColorSource) new FileColorSource(new BitAddress(PaletteOffset + 16 * metadata.PaletteId + x * 2, 0), Endian.Little))
            .ToList();

        var pal = new Palette("monsterPalette", new ColorFactory(), ColorModel.Bgr15, paletteSources, true, PaletteStorageSource.ProjectXml, dataFile);

        int arrangerWidth = metadata.TileSetSize == TileSetSize.Small ? 8 : 16;
        int arrangerHeight = metadata.TileSetSize == TileSetSize.Small ? 8 : 16;

        var formData = new byte[arrangerWidth * arrangerHeight / 8];
        int formAddress = metadata.TileSetSize == TileSetSize.Small ? FormSmallOffset + 8 * metadata.FormId : FormLargeOffset + 32 * metadata.FormId;

        await dataFile.ReadAsync(new BitAddress(formAddress, 0), formData.Length * 8, formData);
        
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

        int tileOffset = TileSetOffset + 8 * metadata.TileSetId;
        int tileSize = metadata.ColorDepth == TileColorDepth.Bpp4 ? 32 : 24;

        for (int y = 0; y < arrangerHeight; y++)
        {
            for (int x = 0; x < arrangerWidth; x++)
            {
                if (bitStream.ReadBit() == 1)
                {
                    IGraphicsCodec codec = new IndexedCodecPluginAdapter(metadata.ColorDepth == TileColorDepth.Bpp4 ? new Snes4BppCodec() : new Snes3BppCodec(), pal, 8, 8);
                    var element = new ArrangerElement(x * 8, y * 8, dataFile, new BitAddress(tileOffset * 8), codec);
                    tileOffset += tileSize;
                    arranger.SetElement(element, x, y);
                }
            }
        }

        return new SpriteResourceContext(dataFile, arranger);
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
