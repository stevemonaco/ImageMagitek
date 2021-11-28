using ImageMagitek.Colors;
using Stylet;

namespace TileShop.WPF.Models;

public class AssociatePaletteModel : PropertyChangedBase
{
    private string _name;
    public string Name
    {
        get => _name;
        set => SetAndNotify(ref _name, value);
    }

    private string _paletteKey;
    public string PaletteKey
    {
        get => _paletteKey;
        set => SetAndNotify(ref _paletteKey, value);
    }

    public Palette Palette { get; set; }

    public AssociatePaletteModel(Palette palette, string paletteKey)
    {
        Palette = palette;
        Name = Palette.Name;
        PaletteKey = paletteKey;
    }
}
