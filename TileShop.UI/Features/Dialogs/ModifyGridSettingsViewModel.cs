using System;
using System.ComponentModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TileShop.Shared.Interactions;
using TileShop.Shared.Models;
using TileShop.UI.Models;

namespace TileShop.UI.ViewModels;
public sealed partial class ModifyGridSettingsViewModel : RequestViewModel<GridSettingsSnapshot>
{
    private readonly int _defaultWidthSpacing;
    private readonly int _defaultHeightSpacing;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(TryAcceptCommand))]
    private int _widthSpacing;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(TryAcceptCommand))]
    private int _heightSpacing;

    [ObservableProperty] private int _originX;
    [ObservableProperty] private int _originY;
    [ObservableProperty] private Color _lineColor;
    [ObservableProperty] private Color _primaryColor;
    [ObservableProperty] private Color _secondaryColor;

    /// <summary>
    /// Raised with the current settings whenever the user edits a value so the caller can preview them live
    /// </summary>
    public Action<GridSettingsSnapshot>? SettingsChanged { get; set; }

    public ModifyGridSettingsViewModel(GridSettingsSnapshot current, (int Width, int Height) defaultSpacing)
    {
        Title = "Grid Settings";
        (_defaultWidthSpacing, _defaultHeightSpacing) = defaultSpacing;

        _widthSpacing = current.WidthSpacing;
        _heightSpacing = current.HeightSpacing;
        _originX = current.OriginX;
        _originY = current.OriginY;
        _lineColor = current.LineColor;
        _primaryColor = current.PrimaryColor;
        _secondaryColor = current.SecondaryColor;
    }

    public override GridSettingsSnapshot ProduceResult() =>
        new(WidthSpacing, HeightSpacing, OriginX, OriginY, LineColor, PrimaryColor, SecondaryColor);

    protected override bool CanAccept() => WidthSpacing >= 1 && HeightSpacing >= 1;

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        if (e.PropertyName is nameof(WidthSpacing) or nameof(HeightSpacing) or nameof(OriginX) or nameof(OriginY)
            or nameof(LineColor) or nameof(PrimaryColor) or nameof(SecondaryColor))
        {
            if (CanAccept())
                SettingsChanged?.Invoke(ProduceResult());
        }
    }

    [RelayCommand]
    private void ResetSpacing()
    {
        WidthSpacing = _defaultWidthSpacing;
        HeightSpacing = _defaultHeightSpacing;
        OriginX = 0;
        OriginY = 0;
    }

    [RelayCommand]
    private void ResetColors()
    {
        LineColor = Color.Parse(GridPreferences.DefaultLineColor);
        PrimaryColor = Color.Parse(GridPreferences.DefaultPrimaryColor);
        SecondaryColor = Color.Parse(GridPreferences.DefaultSecondaryColor);
    }
}
