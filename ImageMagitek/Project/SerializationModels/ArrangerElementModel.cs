namespace ImageMagitek.Project.SerializationModels
{
    internal class ArrangerElementModel
    {
        public ArrangerElementModel() { }

        public ArrangerElementModel(string dataFileKey, string paletteKey, string formatName, FileBitAddress fileAddress, int positionX, int positionY)
        {
            DataFileKey = dataFileKey;
            PaletteKey = paletteKey;
            FormatName = formatName;
            FileAddress = fileAddress;
            PositionX = positionX;
            PositionY = positionY;
        }

        public string DataFileKey { get; set; }
        public string PaletteKey { get; set; }
        public string FormatName { get; set; }
        public FileBitAddress FileAddress { get; set; }
        public int PositionX { get; set; }
        public int PositionY { get; set; }

        public ArrangerElement ToArrangerElement()
        {
            var el = new ArrangerElement();
            el.DataFileKey = DataFileKey;
            el.PaletteKey = PaletteKey;
            el.FormatName = FormatName;
            el.FileAddress = FileAddress;
            el.X1 = PositionX;
            el.Y1 = PositionY;

            return el;
        }
    }
}
