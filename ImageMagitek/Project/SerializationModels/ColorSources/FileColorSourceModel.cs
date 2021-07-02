namespace ImageMagitek.Project.Serialization
{
    public class FileColorSourceModel : IColorSourceModel
    {
        public FileBitAddress FileAddress { get; set; }
        public int Entries { get; set; }
        public Endian Endian { get; set; }

        public FileColorSourceModel()
        {
        }

        public FileColorSourceModel(FileBitAddress fileAddress, int entries, Endian endian)
        {
            FileAddress = fileAddress;
            Entries = entries;
            Endian = endian;
        }

        public bool ResourceEquals(IColorSourceModel sourceModel)
        {
            if (sourceModel is not FileColorSourceModel model)
                return false;

            return model.FileAddress == FileAddress && model.Entries == Entries && model.Endian == Endian;
        }
    }
}
