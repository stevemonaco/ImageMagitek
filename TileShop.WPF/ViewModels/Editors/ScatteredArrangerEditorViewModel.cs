using System;
using System.Linq;
using Stylet;
using GongSolutions.Wpf.DragDrop;
using ImageMagitek;
using ImageMagitek.Colors;
using TileShop.Shared.Services;
using TileShop.WPF.Imaging;
using TileShop.WPF.Models;
using TileShop.WPF.Behaviors;
using TileShop.Shared.Models;
using TileShop.Shared.EventModels;

namespace TileShop.WPF.ViewModels
{
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
                    CancelOverlay();
                SetAndNotify(ref _activeTool, value);
            }
        }

        public ScatteredArrangerEditorViewModel(Arranger arranger, IEventAggregator events) : this(arranger, events, null) { }

        public ScatteredArrangerEditorViewModel(Arranger arranger, IEventAggregator events, IPaletteService paletteService)
        {
            Resource = arranger;
            _events = events;
            _paletteService = paletteService;
            _defaultPalette = _paletteService?.DefaultPalette;

            _workingArranger = arranger.CloneArranger();

            DisplayName = Resource?.Name ?? "Unnamed Arranger";

            RenderArranger();
            CreateGridlines();

            Overlay = new ArrangerOverlay();

            var arrangerPalettes = _workingArranger.GetReferencedPalettes().OrderBy(x => x.Name).ToList();
            arrangerPalettes.Add(_defaultPalette);
            Palettes = new BindableCollection<PaletteModel>(arrangerPalettes.Select(x => new PaletteModel(x)));
            ActivePalette = Palettes.First();
        }

        public void SetSelectToolMode() => ActiveTool = ScatteredArrangerTool.Select;

        public void SetApplyPaletteMode() => ActiveTool = ScatteredArrangerTool.ApplyPalette;

        public override void SaveChanges()
        {
            if (_workingArranger.ColorType == PixelColorType.Indexed)
                _indexedImage.SaveImage();
            else if (_workingArranger.ColorType == PixelColorType.Direct)
                _directImage.SaveImage();

            // TODO: Save _workingArranger elements to project tree
        }

        public override void DiscardChanges()
        {
            _workingArranger = (Resource as Arranger).CloneArranger();
            RenderArranger();
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
            if (_workingArranger.ColorType == PixelColorType.Indexed)
            {
                _indexedImage = new IndexedImage(_workingArranger);
                ArrangerSource = new IndexedImageSource(_indexedImage, _workingArranger, _defaultPalette);
            }
            else if (_workingArranger.ColorType == PixelColorType.Direct)
            {

                _directImage = new DirectImage(_workingArranger);
                ArrangerSource = new DirectImageSource(_directImage);
            }
        }

        private bool TryApplyPalette(int pixelX, int pixelY, Palette palette)
        {
            var elX = pixelX / _workingArranger.ElementPixelSize.Width;
            var elY = pixelY / _workingArranger.ElementPixelSize.Height;

            if (elX >= _workingArranger.ArrangerElementSize.Width || elY >= _workingArranger.ArrangerElementSize.Height)
                return false;

            var el = _workingArranger.GetElement(elX, elY);

            if (ReferenceEquals(palette, el.Palette))
                return false;

            if (ReferenceEquals(_defaultPalette, palette))
                el = el.WithPalette(null);
            else
                el = el.WithPalette(palette);

            if (_workingArranger.GetElement(elX, elY).Palette == el.Palette)
                return false;

            _workingArranger.SetElement(el, elX, elY);
            RenderArranger();
            IsModified = true;

            return true;
        }

        private bool TryPickPalette(int pixelX, int pixelY)
        {
            var elX = pixelX / _workingArranger.ElementPixelSize.Width;
            var elY = pixelY / _workingArranger.ElementPixelSize.Height;

            if (elX >= _workingArranger.ArrangerElementSize.Width || elY >= _workingArranger.ArrangerElementSize.Height)
                return false;

            var el = _workingArranger.GetElement(elX, elY);

            ActivePalette = Palettes.FirstOrDefault(x => ReferenceEquals(el.Palette, x.Palette)) ?? Palettes.First(x => ReferenceEquals(_defaultPalette, x.Palette));
            return true;
        }

        public override void CancelOverlay()
        {
            CanPastePixels = false;
            CanPasteElements = false;
            Overlay.Cancel();
        }

        protected override bool CanAcceptTransfer(ArrangerTransferModel model)
        {
            CanPasteElements = CanAcceptElementTransfer(model);
            CanPastePixels = CanAcceptPixelTransfer(model);

            return CanPasteElements || CanPastePixels;


            //bool canAccept = false;
            //bool isCompatibleSize = false;

            //Source must fit onto the target
            //if (model.Arranger.Layout == ArrangerLayout.Single)
            //{
            //    if (_workingArranger.ArrangerPixelSize.Width < model.Width || _workingArranger.ArrangerPixelSize.Height < model.Height)
            //        return false;
            //}
            //else if (model.Arranger.Layout == ArrangerLayout.Tiled)
            //{
            //    if (_workingArranger.ArrangerPixelSize.Width < model.Width || _workingArranger.ArrangerPixelSize.Height < model.Height)
            //        return false;
            //}

            //var sizeRules =
            //    (SourceMode: model.Arranger.Mode, TargetMode: _arranger.Mode, CopyMode: DropCopy,
            //if (model.Arranger.ArrangerElementSize == _arranger.ArrangerElementSize)
            //    isCompatibleSize = true;

            //var copyRules =
            //    (SourceMode: model.Arranger.Mode, TargetMode: _arranger.Mode, CopyMode: DropCopy, SourceLayout: model.Arranger.Layout, TargetLayout: _arranger.Layout);

            //switch (copyRules)
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

            //return canAccept;
        }

        private bool CanAcceptPixelTransfer(ArrangerTransferModel model) => true;

        private bool CanAcceptElementTransfer(ArrangerTransferModel model)
        {
            // Ensure elements are an even multiple width/height
            if (model.Width % _workingArranger.ElementPixelSize.Width != 0 || model.Height % _workingArranger.ElementPixelSize.Height != 0)
                return false;

            // Ensure start point is aligned to an element boundary
            if (model.X % _workingArranger.ElementPixelSize.Width != 0 || model.Y % _workingArranger.ElementPixelSize.Height != 0)
                return false;

            return true;
        }

        public void ApplyPasteAsPixels()
        {

        }

        public void ApplyPasteAsElements()
        {
            var sourceArranger = Overlay.CopyArranger;
            var sourceStart = new System.Drawing.Point(Overlay.SelectionRect.SnappedLeft / sourceArranger.ElementPixelSize.Width,
                Overlay.SelectionRect.SnappedTop / sourceArranger.ElementPixelSize.Height);
            var destStart = new System.Drawing.Point(Overlay.PasteRect.SnappedLeft / _workingArranger.ElementPixelSize.Width,
                Overlay.PasteRect.SnappedTop / _workingArranger.ElementPixelSize.Height);
            int copyWidth = Overlay.SelectionRect.SnappedWidth / sourceArranger.ElementPixelSize.Width;
            int copyHeight = Overlay.SelectionRect.SnappedHeight / sourceArranger.ElementPixelSize.Height;

            var result = ElementCopier.CopyElements(sourceArranger, _workingArranger as ScatteredArranger, sourceStart, destStart, copyWidth, copyHeight);

            var notifyEvent = result.Match(
                success => new NotifyOperationEvent("Paste successfully applied"),
                fail => new NotifyOperationEvent(fail.Reason)
                );

            _events.PublishOnUIThread(notifyEvent);
            CancelOverlay();
            RenderArranger();
        }
    }
}
