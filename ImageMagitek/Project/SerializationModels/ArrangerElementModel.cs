namespace ImageMagitek.Project.Serialization;

public class ArrangerElementModel
{
    public required string DataFileKey { get; init; }
    public required string? PaletteKey { get; init; }
    public required string CodecName { get; init; }
    public BitAddress FileAddress { get; set; }
    public int PositionX { get; set; }
    public int PositionY { get; set; }
    public MirrorOperation Mirror { get; set; }
    public RotationOperation Rotation { get; set; }

    public bool ResourceEquals(ArrangerElementModel model)
    {
        if (model is null)
            return false;

        return model.FileAddress == FileAddress && model.DataFileKey == DataFileKey && model.PaletteKey == PaletteKey &&
            model.CodecName == CodecName && model.PositionX == PositionX && model.PositionY == PositionY &&
            model.Mirror == Mirror & model.Rotation == Rotation;
    }
}
