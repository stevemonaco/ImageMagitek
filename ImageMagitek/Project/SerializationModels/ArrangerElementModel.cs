namespace ImageMagitek.Project.Serialization;

public class ArrangerElementModel
{
    public string DataFileKey { get; set; }
    public string PaletteKey { get; set; }
    public string CodecName { get; set; }
    public FileBitAddress FileAddress { get; set; }
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
