namespace ImageMagitek.Project.SerializationModels
{
    internal class ArrangerElementModel
    {
        public string DataFileKey { get; set; }
        public string PaletteKey { get; set; }
        public string CodecName { get; set; }
        public FileBitAddress FileAddress { get; set; }
        public int PositionX { get; set; }
        public int PositionY { get; set; }

        public ArrangerElement ToArrangerElement() =>
            new ArrangerElement(PositionX, PositionY, null, FileAddress, null, null);

        public static ArrangerElementModel FromArrangerElement(in ArrangerElement el)
        {
            return new ArrangerElementModel()
            {
                FileAddress = el.FileAddress,
                PositionX = el.X1 / el.Width,
                PositionY = el.Y1 / el.Height,
                CodecName = el.Codec.Name
            };
        }

        public static ArrangerElementModel FromArrangerElement(in ArrangerElement el, int elemX, int elemY)
        {
            return new ArrangerElementModel()
            {
                FileAddress = el.FileAddress,
                PositionX = elemX,
                PositionY = elemY,
                CodecName = el.Codec.Name
            };
        }
    }
}
