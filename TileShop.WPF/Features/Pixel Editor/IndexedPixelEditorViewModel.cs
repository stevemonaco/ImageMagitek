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

namespace TileShop.WPF.ViewModels
{
    public class IndexedPixelEditorViewModel : PixelEditorViewModel<byte>
    {
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
                var colors = Math.Min(256, 1 << _workingArranger.EnumerateElements().Where(x => ReferenceEquals(pal, x.Palette)).Select(x => x.Codec?.ColorDepth ?? 0).Max());
                Palettes.Add(new PaletteModel(pal, colors));
            }

            var defaultPaletteElements = _workingArranger.EnumerateElements().Where(x => x.Palette is null).ToArray();
            var defaultPalette = _paletteService.DefaultPalette;

            if (defaultPaletteElements.Length > 0)
            {
                var defaultColors = Math.Min(256, 1 << defaultPaletteElements.Select(x => x.Codec?.ColorDepth ?? 0).Max());
                Palettes.Add(new PaletteModel(defaultPalette, defaultColors));
            }

            _indexedImage = new IndexedImage(_workingArranger, _viewX, _viewY, _viewWidth, _viewHeight);
            BitmapAdapter = new IndexedBitmapAdapter(_indexedImage);

            DisplayName = $"Pixel Editor - {_workingArranger.Name}";

            CreateGridlines();
            ActivePalette = Palettes.First();
            PrimaryColor = 0;
            SecondaryColor = 1;
            NotifyOfPropertyChange(() => CanRemapColors);
        }

        protected override void Render() => BitmapAdapter.Invalidate();

        protected override void ReloadImage() => _indexedImage.Render();

        public override void SaveChanges()
        {
            try
            {
                _indexedImage.SaveImage();
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

            var colors = Math.Min(256, 1 << _workingArranger.EnumerateElements().Select(x => x.Codec?.ColorDepth ?? 0).Max());

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
