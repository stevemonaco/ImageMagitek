namespace ImageMagitek.Project.Serialization
{
    public class FileColorSourceModel : IColorSourceModel
    {
        public FileBitAddress FileAddress { get; set; }
        public int Entries { get; set; }

        public FileColorSourceModel()
        {
        }

        public FileColorSourceModel(FileBitAddress fileAddress, int entries)
        {
            FileAddress = fileAddress;
            Entries = entries;
        }

        public bool ResourceEquals(IColorSourceModel sourceModel)
        {
            if (sourceModel is not FileColorSourceModel model)
                return false;

            return model.FileAddress == FileAddress && model.Entries == Entries;
        }
    }
}
