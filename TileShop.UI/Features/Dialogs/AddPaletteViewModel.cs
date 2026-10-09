using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using ImageMagitek;
using ImageMagitek.Colors;
using TileShop.Shared.Interactions;
using TileShop.Shared.Models;

namespace TileShop.UI.ViewModels;
public partial class AddPaletteViewModel : RequestViewModel<AddPaletteViewModel>
{
    private readonly Func<string, MagitekResult> _validateName;

    [ObservableProperty] private string _paletteName = "";
    [ObservableProperty] private string? _nameError;
    [ObservableProperty] private ObservableCollection<FileDataSource> _dataSources = new();
    [ObservableProperty] private FileDataSource? _selectedDataSource;
    [ObservableProperty] private ObservableCollection<string> _colorModels = new(Palette.GetColorModelNames());
    [ObservableProperty] private string _selectedColorModel;
    [ObservableProperty] private bool _zeroIndexTransparent;
    [ObservableProperty] private ObservableCollection<Palette?> _templatePalettes = [];

    /// <summary>
    /// Global palette whose colors and color model the new palette starts from, or null for an empty palette
    /// </summary>
    [ObservableProperty] private Palette? _templatePalette;

    public AddPaletteViewModel() : this(_ => MagitekResult.SuccessResult, new())
    {
    }

    public AddPaletteViewModel(Func<string, MagitekResult> validateName, AddPalettePreferences preferences)
    {
        _validateName = validateName;
        Title = "Add a New Palette";
        AcceptName = "Add";

        _selectedColorModel = ColorModels.Contains(preferences.ColorModel) ? preferences.ColorModel : ColorModels.First();
        _zeroIndexTransparent = preferences.ZeroIndexTransparent;
        UpdateNameError();
    }

    partial void OnPaletteNameChanged(string value) => UpdateNameError();

    private void UpdateNameError()
    {
        var result = _validateName(PaletteName);
        NameError = result.HasFailed ? result.AsError.Reason : null;
        TryAcceptCommand.NotifyCanExecuteChanged();
    }

    partial void OnTemplatePaletteChanged(Palette? value)
    {
        if (value is not null)
            SelectedColorModel = value.ColorModel.ToString();
    }

    protected override bool CanAccept() => NameError is null;

    public override AddPaletteViewModel? ProduceResult() => this;

    public AddPalettePreferences ToPreferences() => new(SelectedColorModel, ZeroIndexTransparent);
}
