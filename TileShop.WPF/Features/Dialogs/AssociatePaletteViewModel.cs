using Stylet;
using System.Collections.Generic;
using System.Linq;
using TileShop.WPF.Models;

namespace TileShop.WPF.ViewModels;

public class AssociatePaletteViewModel : Screen
{
    private BindableCollection<AssociatePaletteModel> _palettes;
    public BindableCollection<AssociatePaletteModel> Palettes
    {
        get => _palettes;
        set => SetAndNotify(ref _palettes, value);
    }

    private AssociatePaletteModel _selectedPalette;
    public AssociatePaletteModel SelectedPalette
    {
        get => _selectedPalette;
        set => SetAndNotify(ref _selectedPalette, value);
    }

    public AssociatePaletteViewModel(IEnumerable<AssociatePaletteModel> palettes)
    {
        Palettes = new BindableCollection<AssociatePaletteModel>(palettes);
        SelectedPalette = Palettes.First();
    }

    public void Associate() => RequestClose(true);

    public void Cancel() => RequestClose(false);
}
