using ImageMagitek.Colors;
using System;

namespace ImageMagitek.Project.Serialization
{
    public class PaletteModel : ResourceModel
    {
        public ColorModel ColorModel { get; set; }
        public string DataFileKey { get; set; }
        public FileBitAddress FileAddress { get; set; }
        public int Entries { get; set; }
        public bool ZeroIndexTransparent { get; set; }
        public PaletteStorageSource StorageSource { get; set; }

        public override bool ResourceEquals(ResourceModel resourceModel)
        {
            if (resourceModel is not PaletteModel model)
                return false;

            return model.ColorModel == ColorModel && model.DataFileKey == DataFileKey && model.FileAddress == FileAddress &&
                model.Entries == Entries && model.ZeroIndexTransparent == ZeroIndexTransparent && model.StorageSource == StorageSource
                && model.Name == Name;
        }
    }
}
