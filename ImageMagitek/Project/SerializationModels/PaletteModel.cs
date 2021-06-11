using ImageMagitek.Colors;
using System.Collections.Generic;
using System.Linq;

namespace ImageMagitek.Project.Serialization
{
    public class PaletteModel : ResourceModel
    {
        public ColorModel ColorModel { get; set; }
        public string DataFileKey { get; set; }
        public int Entries { get; set; }
        public bool ZeroIndexTransparent { get; set; }
        public PaletteStorageSource StorageSource { get; set; }
        public List<IColorSourceModel> ColorSources { get; set; } = new();

        public override bool ResourceEquals(ResourceModel resourceModel)
        {
            if (resourceModel is not PaletteModel model)
                return false;

            return model.ColorModel == ColorModel && model.DataFileKey == DataFileKey && model.Entries == Entries 
                && model.ZeroIndexTransparent == ZeroIndexTransparent && model.StorageSource == StorageSource
                && model.Name == Name && model.ColorSources.Count == ColorSources.Count &&
                ColorSources.Zip(model.ColorSources).All(x => x.First.ResourceEquals(x.Second));
        }
    }
}
