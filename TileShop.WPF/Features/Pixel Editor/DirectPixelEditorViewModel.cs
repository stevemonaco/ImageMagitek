using System;
using Stylet;
using ImageMagitek;
using ImageMagitek.Colors;
using ImageMagitek.Services;
using TileShop.WPF.Imaging;
using TileShop.WPF.Models;

namespace TileShop.WPF.ViewModels
{
    public class DirectPixelEditorViewModel : PixelEditorViewModel<ColorRgba32>
    {
        public DirectPixelEditorViewModel(Arranger arranger, IEventAggregator events, IWindowManager windowManager, IPaletteService paletteService)
            : base(events, windowManager, paletteService)
        {
            Initialize(arranger, 0, 0, arranger.ArrangerPixelSize.Width, arranger.ArrangerPixelSize.Height);
        }

        public DirectPixelEditorViewModel(Arranger arranger, int viewX, int viewY, int viewWidth, int viewHeight,
            IEventAggregator events, IWindowManager windowManager, IPaletteService paletteService)
            : base(events, windowManager, paletteService)
        {
            Initialize(arranger, viewX, viewY, viewWidth, viewHeight);
        }

        public void Initialize(Arranger arranger, int viewX, int viewY, int viewWidth, int viewHeight)
        {
            Resource = arranger;
            _workingArranger = arranger.CloneArranger();
            _viewX = viewX;
            _viewY = viewY;
            _viewWidth = viewWidth;
            _viewHeight = viewHeight;

            _directImage = new DirectImage(_workingArranger);
            //ArrangerSource = new DirectImageSource(_directImage, _viewX, _viewY, _viewWidth, _viewHeight);

            DisplayName = $"Pixel Editor - {_workingArranger.Name}";

            PrimaryColor = new ColorRgba32(255, 255, 255, 255);
            SecondaryColor = new ColorRgba32(0, 0, 0, 255);
            CreateGridlines();
        }

        protected override void Render() { }
            //ArrangerSource = new DirectImageSource(_directImage, _viewX, _viewY, _viewWidth, _viewHeight);

        protected override void ReloadImage() => _directImage.Render();

        public override void ApplyAction(HistoryAction action)
        {
            if (action is PencilHistoryAction<ColorRgba32> pencilAction)
            {
                foreach (var point in pencilAction.ModifiedPoints)
                {
                    _directImage.SetPixel(point.X, point.Y, pencilAction.PencilColor);
                }
            }
        }

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
    }
}
