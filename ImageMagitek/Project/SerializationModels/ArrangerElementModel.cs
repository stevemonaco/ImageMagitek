namespace ImageMagitek.Project.Serialization;

public sealed record ArrangerElementModel
{
    public required string DataFileKey { get; init; }
    public required string? PaletteKey { get; init; }
    public required string CodecName { get; init; }
    public BitAddress FileAddress { get; init; }
    public int PositionX { get; init; }
    public int PositionY { get; init; }
    public MirrorOperation Mirror { get; init; }
    public RotationOperation Rotation { get; init; }
}
