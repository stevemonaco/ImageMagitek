namespace ImageMagitek.Project
{
    public class DataFileNode : ResourceNode<DataFile>
    {
        public DataFile DataFile { get; set; }

        public DataFileNode(string name, DataFile dataFile) : base(name, dataFile)
        {
            DataFile = dataFile;
        }
    }
}
