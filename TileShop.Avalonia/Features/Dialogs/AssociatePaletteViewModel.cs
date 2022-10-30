using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using TileShop.AvaloniaUI.Windowing;
using TileShop.Shared.Models;

namespace TileShop.AvaloniaUI.ViewModels;

public partial class AssociatePaletteViewModel : DialogViewModel<AssociatePaletteViewModel>
{
    [ObservableProperty] private ObservableCollection<AssociatePaletteModel> _palettes;
    [ObservableProperty] private AssociatePaletteModel _selectedPalette;

    public AssociatePaletteViewModel(IEnumerable<AssociatePaletteModel> palettes)
    {
        _palettes = new(palettes);
        _selectedPalette = Palettes.First();
        Title = "Associate a Palette with Arranger";
        AcceptName = "Associate";
    }

    protected override void Accept()
    {
        _requestResult = this;
        OnPropertyChanged(nameof(RequestResult));
    }
}
