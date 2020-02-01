using Caliburn.Micro;
using ImageMagitek;
using System;
using TileShop.WPF.Behaviors;
using TileShop.Shared.EventModels;
using TileShop.WPF.Helpers;
using TileShop.WPF.Models;
using System.Drawing;
using GongSolutions.Wpf.DragDrop;
using TileShop.Shared.Models;
using System.Linq;
using TileShop.WPF.Imaging;

namespace TileShop.WPF.ViewModels
{
    public enum EditMode { ArrangeGraphics, ModifyGraphics }

    public class ScatteredArrangerEditorViewModel : ArrangerEditorViewModel, IDropTarget
    {
        public ScatteredArrangerEditorViewModel(Arranger arranger, IEventAggregator events)
        {
            Resource = arranger;
            _arranger = arranger;
            _events = events;

            if (arranger.ColorType == PixelColorType.Indexed)
            {
                _indexedImage = new IndexedImage(arranger);
                ArrangerSource = new IndexedImageSource(_indexedImage, _arranger, null);
            }
            else if (arranger.ColorType == PixelColorType.Direct)
            {
                _directImage = new DirectImage(arranger);
                ArrangerSource = new DirectImageSource(_directImage);
            }

            CreateGridlines();

            if (arranger.Layout == ArrangerLayout.TiledArranger)
                Selection = new ArrangerSelector(_arranger.ArrangerPixelSize, _arranger.ElementPixelSize, SnapMode.Element);
            else
                Selection = new ArrangerSelector(_arranger.ArrangerPixelSize, _arranger.ElementPixelSize, SnapMode.Pixel);
        }

        public override bool SaveChanges()
        {
            return true;
        }

        public override bool DiscardChanges()
        {
            return true;
        }

        public void DragOver(IDropInfo dropInfo)
        {
            throw new NotImplementedException();
        }

        public void Drop(IDropInfo dropInfo)
        {
            throw new NotImplementedException();
        }
    }
}
