using ImageMagitek.Colors;
using Stylet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using TileShop.WPF.Models;

namespace TileShop.WPF.ViewModels;

public class TableColorViewModel : EditableColorBaseViewModel
{
    private ITableColor _foreignColor;
    private readonly IColorFactory _colorFactory;

    public override bool CanSaveColor
    {
        get => WorkingColor.Color != _foreignColor.Color;
    }

    private BindableCollection<ValidatedTableColorModel> _availableColors = new();
    public BindableCollection<ValidatedTableColorModel> AvailableColors
    {
        get => _availableColors;
        set => SetAndNotify(ref _availableColors, value);
    }

    public TableColorViewModel(ITableColor foreignColor, int index, IColorFactory colorFactory)
    {
        _foreignColor = foreignColor;
        Index = index;
        _colorFactory = colorFactory;

        WorkingColor = (ITableColor)_colorFactory.CloneColor(foreignColor);
        var nativeColor = _colorFactory.ToNative(foreignColor);
        Color = Color.FromArgb(nativeColor.A, nativeColor.R, nativeColor.G, nativeColor.B);

        AvailableColors.AddRange(CreateTableColorModels());
    }

    public void SaveColor()
    {
        _foreignColor = (ITableColor)_colorFactory.CloneColor(WorkingColor);
        OnPropertyChanged(nameof(CanSaveColor));
    }

    public void SetWorkingColor(ValidatedTableColorModel model)
    {
        WorkingColor = (ITableColor)_colorFactory.CloneColor(model.WorkingColor);
        var nativeColor = _colorFactory.ToNative(WorkingColor);
        Color = Color.FromArgb(nativeColor.A, nativeColor.R, nativeColor.G, nativeColor.B);
        OnPropertyChanged(nameof(CanSaveColor));
    }

    //public void MouseOver(ValidatedTableColorModel model)
    //{
    //    string notifyMessage = $"Palette Index: {model.Index}";
    //    var notifyEvent = new NotifyStatusEvent(notifyMessage, NotifyStatusDuration.Indefinite);
    //    _events.PublishOnUIThread(notifyEvent);
    //}

    private IEnumerable<ValidatedTableColorModel> CreateTableColorModels()
    {
        if (_foreignColor is ColorNes)
        {
            for (int i = 0; i < 64; i++)
                yield return new ValidatedTableColorModel(new ColorNes((uint)i), i, _colorFactory);
        }
        else
            throw new NotSupportedException($"Table-based color editing is not supported for color type '{_foreignColor.GetType()}'");
    }
}
