using GongSolutions.Wpf.DragDrop;
using ImageMagitek.Colors;
using Stylet;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Media;
using TileShop.WPF.Models;

namespace TileShop.WPF.ViewModels
{
    public class ColorRemapViewModel : Screen, IDropTarget //, IDropTarget, IDragSource
    {
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
        public ColorRemapViewModel(Palette palette) : this(palette, palette.Entries) { }

        /// <summary>
        /// ViewModel responsible for remapping Palette colors of an indexed image
        /// </summary>
        /// <param name="palette">Palette containing the colors</param>
        /// <param name="paletteEntries">Number of colors to remap starting with the 0-index</param>
        public ColorRemapViewModel(Palette palette, int paletteEntries)
        {
            for (int i = 0; i < paletteEntries; i++)
            {
                var nativeColor = ImageMagitek.Colors.ColorConverter.ToNative(palette[i]);
                var color = Color.FromArgb(nativeColor.A, nativeColor.R, nativeColor.G, nativeColor.B);
                InitialColors.Add(new RemappableColorModel(color, i));
                FinalColors.Add(new RemappableColorModel(color, i));
            }
        }

        public void Remap() => RequestClose(true);

        public void Cancel() => RequestClose(false);

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
}
