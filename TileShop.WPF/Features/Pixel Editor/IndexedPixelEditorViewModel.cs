using System;
using System.Linq;
using Stylet;
using ImageMagitek;
using ImageMagitek.Colors;
using ImageMagitek.Services;
using TileShop.Shared.EventModels;
using TileShop.WPF.Imaging;
using TileShop.WPF.Models;
using Point = System.Drawing.Point;
using ImageMagitek.Image;
using TileShop.Shared.Models;

namespace TileShop.WPF.ViewModels
{
    public class IndexedPixelEditorViewModel : PixelEditorViewModel<byte>
    {
        private IndexedImage _indexedImage;

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

        public IndexedPixelEditorViewModel(Arranger arranger, IEventAggregator events, IWindowManager windowManager, IPaletteService paletteService)
            : base(events, windowManager, paletteService)
        {
            Initialize(arranger, 0, 0, arranger.ArrangerPixelSize.Width, arranger.ArrangerPixelSize.Height);
        }

        public IndexedPixelEditorViewModel(Arranger arranger, int viewX, int viewY, int viewWidth, int viewHeight,
            IEventAggregator events, IWindowManager windowManager, IPaletteService paletteService)
            : base(events, windowManager, paletteService)
        {
            Initialize(arranger, viewX, viewY, viewWidth, viewHeight);
        }

        private void Initialize(Arranger arranger, int viewX, int viewY, int viewWidth, int viewHeight)
        {
            Resource = arranger;
            _workingArranger = arranger.CloneArranger();
            _viewX = viewX;
            _viewY = viewY;
            _viewWidth = viewWidth;
            _viewHeight = viewHeight;

            var arrangerPalettes = _workingArranger.GetReferencedPalettes().OrderBy(x => x.Name).ToArray();
            foreach (var pal in arrangerPalettes)
            {
                var maxArrangerColors = 1 << _workingArranger.EnumerateElements().
                    OfType<ArrangerElement>().
                    Where(x => ReferenceEquals(pal, x.Palette)).Select(x => x.Codec?.ColorDepth ?? 0).
                    Max();

                var colors = Math.Min(256, maxArrangerColors);
                Palettes.Add(new PaletteModel(pal, colors));
            }

            _indexedImage = new IndexedImage(_workingArranger, _viewX, _viewY, _viewWidth, _viewHeight);
            BitmapAdapter = new IndexedBitmapAdapter(_indexedImage);

            DisplayName = $"Pixel Editor - {_workingArranger.Name}";
            SnapMode = SnapMode.Pixel;
            Selection = new ArrangerSelection(arranger, SnapMode);

            CreateGridlines();
            ActivePalette = Palettes.First();
            PrimaryColor = 0;
            SecondaryColor = 1;
            NotifyOfPropertyChange(() => CanRemapColors);
        }

        protected override void Render() => BitmapAdapter.Invalidate();

        protected override void ReloadImage() => _indexedImage.Render();

        public void ConfirmPendingOperation()
        {
            if (Paste?.Copy is ElementCopy || Paste?.Copy is IndexedPixelCopy || Paste?.Copy is DirectPixelCopy)
                ApplyPaste(Paste);
        }

        public override void ApplyPaste(ArrangerPaste paste)
        {
            var notifyEvent = ApplyPasteInternal(paste).Match(
                success =>
                {
                    AddHistoryAction(new PasteArrangerHistoryAction(Paste));

                    IsModified = true;
                    CancelOverlay();
                    BitmapAdapter.Invalidate();

                    return new NotifyOperationEvent("Paste successfully applied");
                },
                fail => new NotifyOperationEvent(fail.Reason)
                );

            _events.PublishOnUIThread(notifyEvent);
        }

        private MagitekResult ApplyPasteInternal(ArrangerPaste paste)
        {
            int destX = Math.Max(0, paste.Rect.SnappedLeft);
            int destY = Math.Max(0, paste.Rect.SnappedTop);
            int sourceX = paste.Rect.SnappedLeft >= 0 ? 0 : -paste.Rect.SnappedLeft;
            int sourceY = paste.Rect.SnappedTop >= 0 ? 0 : -paste.Rect.SnappedTop;

            var destStart = new Point(destX, destY);
            var sourceStart = new Point(sourceX, sourceY);

            ArrangerCopy copy;

            if (paste?.Copy is ElementCopy elementCopy)
                copy = elementCopy.ToPixelCopy();
            else
                copy = paste?.Copy;

            if (copy is IndexedPixelCopy indexedCopy)
            {
                int copyWidth = Math.Min(copy.Width - sourceX, _indexedImage.Width - destX);
                int copyHeight = Math.Min(copy.Height - sourceY, _indexedImage.Height - destY);

                return ImageCopier.CopyPixels(indexedCopy.Image, _indexedImage, sourceStart, destStart, copyWidth, copyHeight,
                    PixelRemapOperation.RemapByExactPaletteColors, PixelRemapOperation.RemapByExactIndex);
            }
            //else if (Paste?.Copy is DirectPixelCopy directCopy)
            //{
            //var sourceImage = (Paste.OverlayImage as DirectBitmapAdapter).Image;
            //result = ImageCopier.CopyPixels(sourceImage, _indexedImage, sourceStart, destStart, copyWidth, copyHeight,
            //    ImageRemapOperation.RemapByExactPaletteColors, ImageRemapOperation.RemapByExactIndex);
            //}
            else
                throw new InvalidOperationException($"{nameof(ApplyPaste)} attempted to copy from an arranger of type {paste.Copy.Source.ColorType} to {_workingArranger.ColorType}");
        }

        public override void SaveChanges()
        {
            try
            {
                _indexedImage.SaveImage();

                UndoHistory.Clear();
                RedoHistory.Clear();
                NotifyOfPropertyChange(() => CanUndo);
                NotifyOfPropertyChange(() => CanRedo);

                IsModified = false;
            }
            catch (Exception ex)
            {
                _windowManager.ShowMessageBox($"Could not save the pixel arranger contents\n{ex.Message}\n{ex.StackTrace}", "Save Error");
            }
        }

        public override void DiscardChanges()
        {
            _indexedImage.Render();
            UndoHistory.Clear();
            RedoHistory.Clear();
            NotifyOfPropertyChange(() => CanUndo);
            NotifyOfPropertyChange(() => CanRedo);
        }

        public override void SetPixel(int x, int y, byte color)
        {
            var modelColor = ActivePalette.Colors[color].Color;
            var palColor = new ColorRgba32(modelColor.R, modelColor.G, modelColor.B, modelColor.A);
            var result = _indexedImage.TrySetPixel(x, y, palColor);

            var notifyEvent = result.Match(
                success =>
                {
                    if (_activePencilHistory.ModifiedPoints.Add(new Point(x, y)))
                    {
                        IsModified = true;
                        BitmapAdapter.Invalidate(x, y, 1, 1);
                    }
                    return new NotifyOperationEvent("");
                },
                fail => new NotifyOperationEvent(fail.Reason)
                );
            _events.PublishOnUIThread(notifyEvent);
        }

        public override byte GetPixel(int x, int y) => _indexedImage.GetPixel(x, y);

        public override void ApplyHistoryAction(HistoryAction action)
        {
            if (action is PencilHistoryAction<byte> pencilAction)
            {
                foreach (var point in pencilAction.ModifiedPoints)
                    _indexedImage.SetPixel(point.X, point.Y, pencilAction.PencilColor);
            }
            else if (action is ColorRemapHistoryAction remapAction)
            {
                _indexedImage.RemapColors(remapAction.FinalColors.Select(x => (byte)x.Index).ToList());
            }
            else if (action is PasteArrangerHistoryAction pasteAction)
            {
                ApplyPasteInternal(pasteAction.Paste);
            }
        }

        public bool CanRemapColors
        {
            get
            {
                var palettes = _workingArranger?.GetReferencedPalettes();
                if (palettes?.Count <= 1)
                    return _workingArranger.GetReferencedCodecs().All(x => x.ColorType == PixelColorType.Indexed);

                return false;
            }
        }

        public void RemapColors()
        {
            var palette = _workingArranger.GetReferencedPalettes().FirstOrDefault();
            if (palette is null)
                palette = _paletteService.DefaultPalette;

            var maxArrangerColors = _workingArranger.EnumerateElements().OfType<ArrangerElement>().Select(x => x.Codec?.ColorDepth ?? 0).Max();
            var colors = Math.Min(256, 1 << maxArrangerColors);

            var remapViewModel = new ColorRemapViewModel(palette, colors);
            if (_windowManager.ShowDialog(remapViewModel) is true)
            {
                var remap = remapViewModel.FinalColors.Select(x => (byte)x.Index).ToList();
                _indexedImage.RemapColors(remap);
                Render();

                var remapAction = new ColorRemapHistoryAction(remapViewModel.InitialColors, remapViewModel.FinalColors);
                UndoHistory.Add(remapAction);
                IsModified = true;
            }
        }
    }
}
