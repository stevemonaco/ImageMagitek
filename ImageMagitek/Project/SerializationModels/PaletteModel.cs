namespace ImageMagitek.Project.SerializationModels
{
    internal class PaletteModel : ProjectNodeModel
    {
        public ColorModel ColorModel { get; set; }
        public string DataFileKey { get; set; }
        public FileBitAddress FileAddress { get; set; }
        public int Entries { get; set; }
        public bool ZeroIndexTransparent { get; set; }
        public PaletteStorageSource StorageSource { get; set; }

        public Palette ToPalette() => new Palette(Name, ColorModel, DataFileKey, FileAddress, Entries, ZeroIndexTransparent, StorageSource);
    }
}
