using Avalonia.Data;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageMagitek.Colors;
using ImageMagitek.Utility.Parsing;

namespace TileShop.UI.ViewModels;

/// <summary>
/// Edits a working copy of one palette color; the host assigns it back through <see cref="SaveColorCommand"/>
/// </summary>
public abstract partial class EditableColorBaseViewModel : ObservableRecipient
{
    protected readonly IColorFactory _colorFactory;
    protected readonly ColorModel _colorModel;
    private IColor _foreignColor;

    public IColor WorkingColor { get; protected set; }

    [ObservableProperty] private Color _color;

    /// <summary>
    /// Color currently stored in the palette, shown beside the working color for comparison
    /// </summary>
    [ObservableProperty] private Color _originalColor;

    [ObservableProperty] private int _index;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    private bool _isReadOnly;

    /// <summary>
    /// Command to save/assign the edited color. Set by the host (PaletteEditor, flyout, etc.)
    /// to decouple the color editor from its container.
    /// </summary>
    public IRelayCommand? SaveColorCommand { get; set; }

    public bool CanSaveColor => WorkingColor.Color != _foreignColor.Color;
    public bool CanSave => !IsReadOnly && CanSaveColor;

    public abstract bool HasAlpha { get; }
    public string ColorModelName => _colorModel.ToString();

    /// <summary>
    /// Working color as an Rgba32 hex string; assigning one approximates it in the palette's color model
    /// </summary>
    public string NativeHex
    {
        get
        {
            var native = _colorFactory.ToNative(WorkingColor);
            return HasAlpha ? $"#{native.R:X2}{native.G:X2}{native.B:X2}{native.A:X2}" : $"#{native.R:X2}{native.G:X2}{native.B:X2}";
        }
        set
        {
            if (!ColorParser.TryParse(NormalizeHex(value), ColorModel.Rgba32, out var native))
                throw new DataValidationException("Expected #RRGGBB or #RRGGBBAA");

            ApplyWorkingColor(_colorFactory.ToForeign((ColorRgba32)native, _colorModel));
        }
    }

    /// <summary>
    /// Working color as the raw hex value stored by the palette's color model
    /// </summary>
    public string ForeignHex
    {
        get => _colorFactory.ToHexString(WorkingColor);
        set
        {
            if (!ColorParser.TryParse(NormalizeHex(value), _colorModel, out var foreign))
                throw new DataValidationException($"Not a valid {_colorModel} color");

            ApplyWorkingColor(foreign);
        }
    }

    protected EditableColorBaseViewModel(IColor foreignColor, int index, IColorFactory colorFactory, ColorModel colorModel)
    {
        _foreignColor = foreignColor;
        _colorFactory = colorFactory;
        _colorModel = colorModel;
        _index = index;

        WorkingColor = _colorFactory.CloneColor(foreignColor);
        _originalColor = ToMediaColor(foreignColor);
        _color = _originalColor;
    }

    public void SaveColor()
    {
        _foreignColor = _colorFactory.CloneColor(WorkingColor);
        OriginalColor = Color;
        NotifyWorkingColorChanged();
    }

    /// <summary>
    /// Replaces the working color and refreshes every view of it
    /// </summary>
    protected virtual void ApplyWorkingColor(IColor color)
    {
        WorkingColor = _colorFactory.CloneColor(color);
        NotifyWorkingColorChanged();
    }

    protected void NotifyWorkingColorChanged()
    {
        Color = ToMediaColor(WorkingColor);
        OnPropertyChanged(nameof(NativeHex));
        OnPropertyChanged(nameof(ForeignHex));
        OnPropertyChanged(nameof(CanSaveColor));
        OnPropertyChanged(nameof(CanSave));
    }

    protected Color ToMediaColor(IColor color)
    {
        var native = _colorFactory.ToNative(color);
        return Color.FromArgb(native.A, native.R, native.G, native.B);
    }

    private static string NormalizeHex(string? text)
    {
        var trimmed = text?.Trim() ?? "";
        return trimmed.StartsWith('#') ? trimmed : "#" + trimmed;
    }
}
