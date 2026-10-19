using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using TileShop.Shared.Interactions;
using TileShop.Shared.Models;

namespace TileShop.UI.ViewModels;
public partial class AssociatePaletteViewModel : RequestViewModel<AssociatePaletteViewModel>
{
    [ObservableProperty] private ObservableCollection<AssociatePaletteModel> _palettes;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(TryAcceptCommand))]
    private AssociatePaletteModel? _selectedPalette;

    public AssociatePaletteViewModel(IEnumerable<AssociatePaletteModel> palettes)
    {
        _palettes = new(palettes);
        _selectedPalette = Palettes.FirstOrDefault();
        Title = "Associate a Palette with Arranger";
        AcceptName = "Associate";
    }

    protected override bool CanAccept() => SelectedPalette is not null;

    public override AssociatePaletteViewModel? ProduceResult() => this;
}
