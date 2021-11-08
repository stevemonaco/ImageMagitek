using System.Windows;
using System.Windows.Media;
using GongSolutions.Wpf.DragDrop;
using Stylet;
using ImageMagitek.Colors;
using TileShop.WPF.Models;

namespace TileShop.WPF.ViewModels;

public class ColorRemapViewModel : Screen, IDropTarget //, IDropTarget, IDragSource
{
    private readonly IColorFactory _colorFactory;

    private BindableCollection<RemappableColorModel> _initialColors = new BindableCollection<RemappableColorModel>();
    public BindableCollection<RemappableColorModel> InitialColors
    {
        get => _initialColors;
        set => SetAndNotify(ref _initialColors, value);
    }

    private BindableCollection<RemappableColorModel> _finalColors = new BindableCollection<RemappableColorModel>();
    public BindableCollection<RemappableColorModel> FinalColors
    {
        get => _finalColors;
        set => SetAndNotify(ref _finalColors, value);
    }

    /// <summary>
    /// ViewModel responsible for remapping Palette colors of an indexed image
    /// </summary>
    /// <param name="palette">Palette containing the colors</param>
    public ColorRemapViewModel(Palette palette, IColorFactory colorFactory) : this(palette, palette.Entries, colorFactory) { }

    /// <summary>
    /// ViewModel responsible for remapping Palette colors of an indexed image
    /// </summary>
    /// <param name="palette">Palette containing the colors</param>
    /// <param name="paletteEntries">Number of colors to remap starting with the 0-index</param>
    /// <param name="colorFactory">Factory to create/convert colors</param>
    public ColorRemapViewModel(Palette palette, int paletteEntries, IColorFactory colorFactory)
    {
        _colorFactory = colorFactory;

        for (int i = 0; i < paletteEntries; i++)
        {
            var nativeColor = _colorFactory.ToNative(palette[i]);
            var color = Color.FromArgb(nativeColor.A, nativeColor.R, nativeColor.G, nativeColor.B);
            InitialColors.Add(new RemappableColorModel(color, i));
            FinalColors.Add(new RemappableColorModel(color, i));
        }
    }

    public void Remap() => RequestClose(true);

    public void Cancel() => RequestClose(false);

    public void DragOver(IDropInfo dropInfo)
    {
        if (dropInfo.Data is RemappableColorModel && dropInfo.TargetItem is RemappableColorModel)
        {
            dropInfo.DropTargetAdorner = DropTargetAdorners.Highlight;
            dropInfo.Effects = DragDropEffects.Copy;
        }
    }

    public void Drop(IDropInfo dropInfo)
    {
        var sourceItem = dropInfo.Data as RemappableColorModel;
        var targetItem = dropInfo.TargetItem as RemappableColorModel;

        targetItem.Index = sourceItem.Index;
        targetItem.Color = sourceItem.Color;
    }

    /*

    public void DragOver(IDropInfo dropInfo)
    {
        var sourceItem = dropInfo.Data as RemappableColorModel;
        var targetItem = dropInfo.TargetItem as RemappableColorModel;

        if (sourceItem is object && targetItem is object)
        {
            dropInfo.DropTargetAdorner = DropTargetAdorners.Highlight;
            dropInfo.Effects = DragDropEffects.Copy;
        }
    }

    public void Drop(IDropInfo dropInfo)
    {
        throw new NotImplementedException();
    }

    public void StartDrag(IDragInfo dragInfo)
    {
        throw new NotImplementedException();
    }

    public bool CanStartDrag(IDragInfo dragInfo)
    {
        throw new NotImplementedException();
    }

    public void Dropped(IDropInfo dropInfo)
    {
        throw new NotImplementedException();
    }

    public void DragDropOperationFinished(DragDropEffects operationResult, IDragInfo dragInfo)
    {
        throw new NotImplementedException();
    }

    public void DragCancelled()
    {
        throw new NotImplementedException();
    }

    public bool TryCatchOccurredException(Exception exception)
    {
        throw new NotImplementedException();
    }
    */
}
