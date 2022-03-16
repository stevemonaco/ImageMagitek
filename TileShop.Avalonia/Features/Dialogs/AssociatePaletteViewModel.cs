using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TileShop.AvaloniaUI.ViewExtenders;
using TileShop.Shared.Models;

namespace TileShop.AvaloniaUI.ViewModels;

public partial class AssociatePaletteViewModel : DialogViewModel<AssociatePaletteViewModel>
{
    [ObservableProperty] private ObservableCollection<AssociatePaletteModel> _palettes;
    [ObservableProperty] private AssociatePaletteModel _selectedPalette;

    public AssociatePaletteViewModel(IEnumerable<AssociatePaletteModel> palettes)
    {
        Palettes = new(palettes);
        SelectedPalette = Palettes.First();
        Title = "Associate a Palette with Arranger";
    }
}
