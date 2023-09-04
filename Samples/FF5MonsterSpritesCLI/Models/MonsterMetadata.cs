namespace FF5MonsterSprites.Models;

public enum TileColorDepth { Bpp4 = 0, Bpp3 = 1}
public enum TileSetSize { Small = 0, Large = 1 }

public record MonsterMetadata(TileColorDepth ColorDepth, int TileSetId, TileSetSize TileSetSize, bool HasShadow, int PaletteId, int FormId, int Unused);