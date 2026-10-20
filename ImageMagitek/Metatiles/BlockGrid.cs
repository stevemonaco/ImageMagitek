namespace ImageMagitek.Metatiles;

/// <summary>
/// A working copy of a metatile map's block numbers that plotting edits and <see cref="MetatileMap.WriteBlocks"/> writes back.
/// A block past the end of the block map data has no number and cannot be edited.
/// </summary>
public sealed class BlockGrid
{
    internal int?[,] Numbers { get; }

    public int Width => Numbers.GetLength(1);
    public int Height => Numbers.GetLength(0);

    internal BlockGrid(int?[,] numbers)
    {
        Numbers = numbers;
    }

    public int? this[int x, int y] => Numbers[y, x];

    /// <returns>False when the block has no number or already holds <paramref name="number"/></returns>
    public bool TrySet(int x, int y, int number)
    {
        if (Numbers[y, x] is not { } current || current == number)
            return false;

        Numbers[y, x] = number;
        return true;
    }

    public BlockGrid Clone() => new((int?[,])Numbers.Clone());
}
