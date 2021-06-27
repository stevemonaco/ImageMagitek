using System;
using Stylet;
using ImageMagitek;
using ImageMagitek.Colors;
using ImageMagitek.Services;
using TileShop.WPF.Imaging;
using TileShop.WPF.Models;
using TileShop.Shared.EventModels;
using ImageMagitek.Image;
using System.Drawing;

namespace TileShop.WPF.ViewModels
{
    public class DirectPixelEditorViewModel : PixelEditorViewModel<ColorRgba32>
    {
        private DirectImage _directImage;

        public DirectPixelEditorViewModel(Arranger arranger, Arranger projectArranger, 
            IEventAggregator events, IWindowManager windowManager, IPaletteService paletteService)
            : base(projectArranger, events, windowManager, paletteService)
        {
            Initialize(arranger, 0, 0, arranger.ArrangerPixelSize.Width, arranger.ArrangerPixelSize.Height);
        }

        public DirectPixelEditorViewModel(Arranger arranger, Arranger projectArranger, int viewX, int viewY, int viewWidth, int viewHeight,
            IEventAggregator events, IWindowManager windowManager, IPaletteService paletteService)
            : base(projectArranger, events, windowManager, paletteService)
        {
            Initialize(arranger, viewX, viewY, viewWidth, viewHeight);
        }

        public void Initialize(Arranger arranger, int viewX, int viewY, int viewWidth, int viewHeight)
        {
            Resource = arranger;
            WorkingArranger = arranger.CloneArranger();
            _viewX = viewX;
            _viewY = viewY;
            _viewWidth = viewWidth;
            _viewHeight = viewHeight;

            _directImage = new DirectImage(WorkingArranger, _viewX, _viewY, _viewWidth, _viewHeight);
            BitmapAdapter = new DirectBitmapAdapter(_directImage);

            DisplayName = $"Pixel Editor - {WorkingArranger.Name}";

            PrimaryColor = new ColorRgba32(255, 255, 255, 255);
            SecondaryColor = new ColorRgba32(0, 0, 0, 255);
            CreateGridlines();
        }

        public override void Render() { }
            //ArrangerSource = new DirectImageSource(_directImage, _viewX, _viewY, _viewWidth, _viewHeight);

        protected override void ReloadImage() => _directImage.Render();

        public override void SaveChanges()
        {
            try
            {
                _directImage.SaveImage();
                IsModified = false;
            }
            catch (Exception ex)
            {
                _windowManager.ShowMessageBox($"Could not save the pixel arranger contents\n{ex.Message}\n{ex.StackTrace}", "Save Error");
            }
        }

        public override void DiscardChanges()
        {
            _directImage.Render();
            UndoHistory.Clear();
            RedoHistory.Clear();
            NotifyOfPropertyChange(() => CanUndo);
            NotifyOfPropertyChange(() => CanRedo);
        }

        public override ColorRgba32 GetPixel(int x, int y) => _directImage.GetPixel(x, y);

        public override void SetPixel(int x, int y, ColorRgba32 color)
        {
            _directImage.SetPixel(x + _viewX, y + _viewY, color);
        }

        public override void ApplyHistoryAction(HistoryAction action)
        {
            if (action is PencilHistoryAction<ColorRgba32> pencilAction)
            {
                foreach (var point in pencilAction.ModifiedPoints)
                {
                    _directImage.SetPixel(point.X, point.Y, pencilAction.PencilColor);
                }
            }
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

        public MagitekResult ApplyPasteInternal(ArrangerPaste paste)
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
                int copyWidth = Math.Min(copy.Width - sourceX, _directImage.Width - destX);
                int copyHeight = Math.Min(copy.Height - sourceY, _directImage.Height - destY);

                return ImageCopier.CopyPixels(indexedCopy.Image, _directImage, sourceStart, destStart, copyWidth, copyHeight);
            }
            else if (copy is DirectPixelCopy directCopy)
            {
                int copyWidth = Math.Min(copy.Width - sourceX, _directImage.Width - destX);
                int copyHeight = Math.Min(copy.Height - sourceY, _directImage.Height - destY);

                return ImageCopier.CopyPixels(directCopy.Image, _directImage, sourceStart, destStart, copyWidth, copyHeight);
            }
            else
                throw new InvalidOperationException($"{nameof(ApplyPasteInternal)} attempted to copy from an arranger of type {Paste.Copy.Source.ColorType} to {WorkingArranger.ColorType}");
        }

        public override void FloodFill(int x, int y, ColorRgba32 fillColor)
        {
            throw new NotImplementedException();
        }
    }
}
