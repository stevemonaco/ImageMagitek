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

        public ArrangerElement ToArrangerElement()
        {
            return new ArrangerElement
            {
                FileAddress = FileAddress,
                X1 = PositionX,
                Y1 = PositionY
            };
        }

        public static ArrangerElementModel FromArrangerElement(ArrangerElement el)
        {
            return new ArrangerElementModel()
            {
                FileAddress = el.FileAddress,
                PositionX = el.X1 / el.Width,
                PositionY = el.Y1 / el.Height,
                CodecName = el.Codec.Name
            };
        }

        public static ArrangerElementModel FromArrangerElement(ArrangerElement el, int positionX, int positionY)
        {
            return new ArrangerElementModel()
            {
                FileAddress = el.FileAddress,
                PositionX = positionX,
                PositionY = positionY,
                CodecName = el.Codec.Name
            };
        }
    }
}
