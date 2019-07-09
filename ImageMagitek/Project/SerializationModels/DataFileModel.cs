namespace ImageMagitek.Project.SerializationModels
{
    internal class DataFileModel : ProjectNodeModel
    {
        public string Location { get; set; }

        public DataFile ToDataFile() => new DataFile(Name, Location);
    }
}
