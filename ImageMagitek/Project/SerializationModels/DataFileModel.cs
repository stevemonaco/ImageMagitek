namespace ImageMagitek.Project.SerializationModels
{
    internal class DataFileModel : ProjectNodeModel
    {
        public string Location { get; set; }

        public DataFile ToDataFile() => new DataFile(Name, Location);

        public static DataFileModel FromDataFile(DataFile df)
        {
            return new DataFileModel()
            {
                Name = df.Name,
                Location = df.Location
            };
        }
    }
}
