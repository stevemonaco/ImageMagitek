using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using TileShop.Shared.Models;
using TileShop.UI.Controls;

namespace TileShop.UI.ViewModels;

public partial class AssociatePaletteViewModel : RequestBaseViewModel<AssociatePaletteViewModel>
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

    public override AssociatePaletteViewModel? ProduceResult() => this;

    protected override void Accept()
    {
        RequestResult = this;
        OnPropertyChanged(nameof(RequestResult));
    }
}
