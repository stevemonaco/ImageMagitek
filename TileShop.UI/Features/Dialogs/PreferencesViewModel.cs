using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageMagitek.Services;
using TileShop.Shared.Interactions;
using TileShop.Shared.Models;
using TileShop.Shared.Services;
using TileShop.UI.Models;

namespace TileShop.UI.ViewModels;

public sealed partial class PreferencesViewModel : RequestViewModel<PreferencesViewModel>
{
    [ObservableProperty] private ThemeStyle _theme;
    [ObservableProperty] private bool _enableArrangerSymmetryTools;
    [ObservableProperty] private NumericBase _jumpToOffsetBase;
    [ObservableProperty] private Color _lineColor;
    [ObservableProperty] private Color _primaryColor;
    [ObservableProperty] private Color _secondaryColor;
    [ObservableProperty] private string? _nesPalette;

    public IReadOnlyList<ThemeStyle> Themes { get; } = Enum.GetValues<ThemeStyle>();
    public IReadOnlyList<NumericBase> NumericBases { get; } = Enum.GetValues<NumericBase>();
    public IReadOnlyList<string> NesPaletteNames { get; }

    /// <param name="nesPalette">Name of the NES master palette currently in effect</param>
    public PreferencesViewModel(UserPreferences preferences, string nesPalette)
    {
        Title = "Preferences";

        _theme = preferences.Theme;
        _enableArrangerSymmetryTools = preferences.EnableArrangerSymmetryTools;
        _jumpToOffsetBase = preferences.JumpToOffsetBase;
        _lineColor = GridSettingsViewModel.ParseHex(preferences.Grid.LineColor, GridPreferences.DefaultLineColor);
        _primaryColor = GridSettingsViewModel.ParseHex(preferences.Grid.PrimaryColor, GridPreferences.DefaultPrimaryColor);
        _secondaryColor = GridSettingsViewModel.ParseHex(preferences.Grid.SecondaryColor, GridPreferences.DefaultSecondaryColor);
        _nesPalette = nesPalette;

        NesPaletteNames = Directory.EnumerateFiles(BootstrapService.DefaultPalettePath, "*.json")
            .Select(Path.GetFileNameWithoutExtension)
            .OfType<string>()
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public override PreferencesViewModel ProduceResult() => this;

    [RelayCommand]
    private void ResetColors()
    {
        LineColor = Color.Parse(GridPreferences.DefaultLineColor);
        PrimaryColor = Color.Parse(GridPreferences.DefaultPrimaryColor);
        SecondaryColor = Color.Parse(GridPreferences.DefaultSecondaryColor);
    }
}
