using System;
using System.Linq;
using System.Windows;
using Stylet;
using GongSolutions.Wpf.DragDrop;
using ImageMagitek;
using ImageMagitek.Colors;
using TileShop.Shared.Services;
using TileShop.WPF.Helpers;
using TileShop.WPF.Imaging;
using TileShop.WPF.Models;
using TileShop.WPF.Behaviors;
using TileShop.Shared.Models;

namespace TileShop.WPF.ViewModels
{
    public enum EditMode { ArrangeGraphics, ModifyGraphics }

    public enum DropCopyMode { Elements, Pixels }

    public enum ScatteredArrangerTool { Select, ApplyPalette, PickPalette }

    public class ScatteredArrangerEditorViewModel : ArrangerEditorViewModel, IDropTarget, IDragSource
    {
        private IPaletteService _paletteService;
        private Palette _defaultPalette;

        private BindableCollection<PaletteModel> _palettes = new BindableCollection<PaletteModel>();
        public BindableCollection<PaletteModel> Palettes
        {
            get => _palettes;
            set => SetAndNotify(ref _palettes, value);
        }

        private PaletteModel _activePalette;
        public PaletteModel ActivePalette
        {
            get => _activePalette;
            set => SetAndNotify(ref _activePalette, value);
        }

        private ScatteredArrangerTool _activeTool = ScatteredArrangerTool.Select;
        public ScatteredArrangerTool ActiveTool
        {
            get => _activeTool;
            set
            {
                if (value != ScatteredArrangerTool.Select)
                    CancelSelection();
                SetAndNotify(ref _activeTool, value);
            }
        }

        private DropCopyMode _dropCopy = DropCopyMode.Elements;
        public DropCopyMode DropCopy
        {
            get => _dropCopy;
            set => SetAndNotify(ref _dropCopy, value);
        }

        public ScatteredArrangerEditorViewModel(Arranger arranger, IEventAggregator events) : this(arranger, events, null) { }

        public ScatteredArrangerEditorViewModel(Arranger arranger, IEventAggregator events, IPaletteService paletteService)
        {
            Resource = arranger;
            _arranger = arranger;
            _events = events;
            _paletteService = paletteService;
            _defaultPalette = _paletteService?.DefaultPalette;

            DisplayName = Resource?.Name ?? "Unnamed Arranger";

            RenderArranger();
            CreateGridlines();

            if (arranger.Layout == ArrangerLayout.Tiled)
            {
                Selection = new ArrangerSelectionRegion(_arranger.ArrangerPixelSize, _arranger.ElementPixelSize, SnapMode.Element);
                DropCopy = DropCopyMode.Elements;
            }
            else if (arranger.Layout == ArrangerLayout.Single)
            {
                Selection = new ArrangerSelectionRegion(_arranger.ArrangerPixelSize, _arranger.ElementPixelSize, SnapMode.Pixel);
                DropCopy = DropCopyMode.Pixels;
            }

            var arrangerPalettes = _arranger.GetReferencedPalettes().OrderBy(x => x.Name).ToList();
            arrangerPalettes.Add(_defaultPalette);
            Palettes = new BindableCollection<PaletteModel>(arrangerPalettes.Select(x => new PaletteModel(x)));
            ActivePalette = Palettes.First();
        }

        public void SetSelectToolMode() => ActiveTool = ScatteredArrangerTool.Select;

        public void SetApplyPaletteMode() => ActiveTool = ScatteredArrangerTool.ApplyPalette;

        public override void SaveChanges()
        {
            throw new NotImplementedException();
        }

        public override void DiscardChanges()
        {
            throw new NotImplementedException();
        }

        public override void OnMouseDown(object sender, MouseCaptureArgs e)
        {
            int x = (int)e.X / Zoom;
            int y = (int)e.Y / Zoom;

            if (ActiveTool == ScatteredArrangerTool.ApplyPalette && e.LeftButton)
                TryApplyPalette(x, y, ActivePalette.Palette);
            else if (ActiveTool == ScatteredArrangerTool.PickPalette && e.LeftButton)
                TryPickPalette(x, y);
            else
                base.OnMouseDown(sender, e);
        }

        public override void OnMouseMove(object sender, MouseCaptureArgs e)
        {
            int x = (int)e.X / Zoom;
            int y = (int)e.Y / Zoom;

            if (ActiveTool == ScatteredArrangerTool.ApplyPalette && e.LeftButton)
                TryApplyPalette(x, y, ActivePalette.Palette);
            else
                base.OnMouseMove(sender, e);
        }

        private void RenderArranger()
        {
            if (_arranger.ColorType == PixelColorType.Indexed)
            {
                _indexedImage = new IndexedImage(_arranger);
                ArrangerSource = new IndexedImageSource(_indexedImage, _arranger, _defaultPalette);
            }
            else if (_arranger.ColorType == PixelColorType.Direct)
            {

                _directImage = new DirectImage(_arranger);
                ArrangerSource = new DirectImageSource(_directImage);
            }
        }

        private bool TryApplyPalette(int pixelX, int pixelY, Palette palette)
        {
            var elX = pixelX / _arranger.ElementPixelSize.Width;
            var elY = pixelY / _arranger.ElementPixelSize.Height;

            if (elX >= _arranger.ArrangerElementSize.Width || elY >= _arranger.ArrangerElementSize.Height)
                return false;

            var el = _arranger.GetElement(elX, elY);

            if (ReferenceEquals(palette, el.Palette))
                return false;

            if (ReferenceEquals(_defaultPalette, palette))
                el = el.WithPalette(null);
            else
                el = el.WithPalette(palette);

            _arranger.SetElement(el, elX, elY);
            RenderArranger();
            IsModified = true;

            return true;
        }

        private bool TryPickPalette(int pixelX, int pixelY)
        {
            var elX = pixelX / _arranger.ElementPixelSize.Width;
            var elY = pixelY / _arranger.ElementPixelSize.Height;

            if (elX >= _arranger.ArrangerElementSize.Width || elY >= _arranger.ArrangerElementSize.Height)
                return false;

            var el = _arranger.GetElement(elX, elY);

            ActivePalette = Palettes.FirstOrDefault(x => ReferenceEquals(el.Palette, x.Palette)) ?? Palettes.First(x => ReferenceEquals(_defaultPalette, x.Palette));
            return true;
        }

        public void DragOver(IDropInfo dropInfo)
        {
            if (dropInfo.Data is ArrangerTransferModel model)
            {
                if (CanAcceptTransfer(model))
                {
                    dropInfo.Effects = DragDropEffects.Copy | DragDropEffects.Move;
                    dropInfo.DropTargetAdorner = DropTargetAdorners.Highlight;
                }
            }
        }

        public bool CanAcceptTransfer(ArrangerTransferModel model)
        {
            bool canAccept = false;
            bool isCompatibleSize = false;

            // Source must fit onto the target
            //if (model.Arranger.Layout == ArrangerLayout.Single)
            //{
            //    if (_arranger.ArrangerPixelSize.Width < model.Width || _arranger.ArrangerPixelSize.Height < model.Height)
            //        return false;
            //}
            //else if (model.Arranger.Layout == ArrangerLayout.Tiled)
            //{
            //    if (_arranger.ArrangerPixelSize.Width < model.Width || _arranger.ArrangerPixelSize.Height < model.Height)
            //}



            //var sizeRules =
            //    (SourceMode: model.Arranger.Mode, TargetMode: _arranger.Mode, CopyMode: DropCopy, 
            //if (model.Arranger.ArrangerElementSize == _arranger.ArrangerElementSize)
            //    isCompatibleSize = true;

            //var copyRules = 
            //    (SourceMode: model.Arranger.Mode, TargetMode: _arranger.Mode, CopyMode: DropCopy, SourceLayout: model.Arranger.Layout, TargetLayout: _arranger.Layout);

            //switch(copyRules)
            //{
            //    case (ArrangerMode.Scattered, ArrangerMode.Scattered, DropCopyMode.Elements, ArrangerLayout.Tiled) 
            //        when model.Arranger.ArrangerElementSize == _arranger.ArrangerElementSize:
                    
            //        canAccept = true;
            //        break;

            //    case (DropCopyMode.Pixels, ArrangerMode.Scattered, ArrangerMode.Scattered):
            //        return true;
            //}

            //if (model.Arranger.Mode == ArrangerMode.Scattered)
            //{
            //    if (model.Arranger.ArrangerElementSize == _arranger.ArrangerElementSize)
            //}

            return canAccept;
        }

        public void Drop(IDropInfo dropInfo)
        {
        }

        public void StartDrag(IDragInfo dragInfo)
        {
            var transferModel = new ArrangerTransferModel(_arranger, Selection.SnappedX1, Selection.SnappedY1, Selection.SnappedWidth, Selection.SnappedHeight);
            dragInfo.Data = transferModel;
            dragInfo.Effects = DragDropEffects.Copy | DragDropEffects.Move;
        }

        public bool CanStartDrag(IDragInfo dragInfo) => true;
        public void Dropped(IDropInfo dropInfo) { }
        public void DragDropOperationFinished(DragDropEffects operationResult, IDragInfo dragInfo) { }
        public void DragCancelled() { }
        public bool TryCatchOccurredException(Exception exception) => false;
    }
}
