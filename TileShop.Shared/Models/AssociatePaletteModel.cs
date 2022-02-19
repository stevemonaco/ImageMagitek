using CommunityToolkit.Mvvm.ComponentModel;
using ImageMagitek.Colors;

namespace TileShop.Shared.Models;

public partial class AssociatePaletteModel : ObservableObject
{
    [ObservableProperty] private string _name;
    [ObservableProperty] private string _paletteKey;
    public Palette Palette { get; set; }

    public AssociatePaletteModel(Palette palette, string paletteKey)
    {
        Palette = palette;
        Name = Palette.Name;
        PaletteKey = paletteKey;
    }
}
