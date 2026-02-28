using System;
using System.Collections.Generic;
using ImageMagitek.Colors;
using Avalonia.Media;
using System.Collections.ObjectModel;
using TileShop.UI.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using TileShop.Shared.Messages;
using CommunityToolkit.Mvvm.Messaging;
using System.Diagnostics.CodeAnalysis;

namespace TileShop.UI.ViewModels;

public partial class TableColorViewModel : EditableColorBaseViewModel
{
    private ITableColor _foreignColor;
    private readonly IColorFactory _colorFactory;

    public override bool CanSaveColor
    {
        get => WorkingColor.Color != _foreignColor.Color;
    }

    [ObservableProperty] private ObservableCollection<ValidatedTableColorModel> _availableColors = new();

    [SetsRequiredMembers]
    public TableColorViewModel(ITableColor foreignColor, int index, IColorFactory colorFactory)
    {
        _foreignColor = foreignColor;
        Index = index;
        _colorFactory = colorFactory;

        WorkingColor = (ITableColor)_colorFactory.CloneColor(foreignColor);
        var nativeColor = _colorFactory.ToNative(foreignColor);
        Color = Color.FromArgb(nativeColor.A, nativeColor.R, nativeColor.G, nativeColor.B);

        AvailableColors = new(CreateTableColorModels());
    }

    public void SaveColor()
    {
        _foreignColor = (ITableColor)_colorFactory.CloneColor(WorkingColor);
        NotifyCanSaveChanged();
    }

    private void NotifyCanSaveChanged()
    {
        OnPropertyChanged(nameof(CanSaveColor));
        OnPropertyChanged(nameof(CanSave));
    }

    public void SetWorkingColor(ValidatedTableColorModel model)
    {
        WorkingColor = (ITableColor)_colorFactory.CloneColor(model.WorkingColor);
        var nativeColor = _colorFactory.ToNative(WorkingColor);
        Color = Color.FromArgb(nativeColor.A, nativeColor.R, nativeColor.G, nativeColor.B);
        NotifyCanSaveChanged();
    }

    public void MouseOver(ValidatedTableColorModel model)
    {
        string notifyMessage = $"Palette Index: {model.Index}";
        var message = new NotifyStatusMessage(notifyMessage, NotifyStatusDuration.Indefinite);
        Messenger.Send(message);
    }

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
