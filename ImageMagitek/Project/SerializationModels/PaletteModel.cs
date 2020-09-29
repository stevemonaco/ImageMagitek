using ImageMagitek.Colors;

namespace ImageMagitek.Project.Serialization
{
    internal class PaletteModel : ProjectNodeModel
    {
        public ColorModel ColorModel { get; set; }
        public string DataFileKey { get; set; }
        public FileBitAddress FileAddress { get; set; }
        public int Entries { get; set; }
        public bool ZeroIndexTransparent { get; set; }
        public PaletteStorageSource StorageSource { get; set; }

        public static PaletteModel FromPalette(Palette pal)
        {
            return new PaletteModel()
            {
                Name = pal.Name,
                ColorModel = pal.ColorModel,
                FileAddress = pal.FileAddress,
                Entries = pal.Entries,
                ZeroIndexTransparent = pal.ZeroIndexTransparent
            };
        }
    }
}
