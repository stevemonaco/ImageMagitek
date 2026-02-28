using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageMagitek.Colors;
using TileShop.UI.ViewModels;

namespace TileShop.UI.Features.Graphics;

public partial class ColorEditorFlyoutViewModel : ObservableObject
{
    private readonly Palette _palette;
    private readonly int _colorIndex;
    private readonly IColorFactory _colorFactory;
    private readonly Action<Palette, int, IColor> _onConfirm;

    [ObservableProperty] private EditableColorBaseViewModel? _colorEditor;

    public bool IsEditable => _palette.StorageSource != PaletteStorageSource.GlobalJson;

    public ColorEditorFlyoutViewModel(Palette palette, int colorIndex, IColorFactory colorFactory,
        Action<Palette, int, IColor> onConfirm)
    {
        _palette = palette;
        _colorIndex = colorIndex;
        _colorFactory = colorFactory;
        _onConfirm = onConfirm;

        var foreignColor = palette.GetForeignColor(colorIndex);
        var isReadOnly = !IsEditable;

        if (foreignColor is IColor32 color32)
        {
            var editor = new Color32ViewModel(color32, colorIndex, colorFactory);
            editor.IsReadOnly = isReadOnly;
            editor.SaveColorCommand = ConfirmCommand;
            ColorEditor = editor;
        }
        else if (foreignColor is ITableColor tableColor)
        {
            var editor = new TableColorViewModel(tableColor, colorIndex, colorFactory);
            editor.IsReadOnly = isReadOnly;
            editor.SaveColorCommand = ConfirmCommand;
            ColorEditor = editor;
        }
    }

    [RelayCommand]
    private void Confirm()
    {
        if (ColorEditor is null)
            return;

        _onConfirm(_palette, _colorIndex, ColorEditor.WorkingColor);
    }
}
