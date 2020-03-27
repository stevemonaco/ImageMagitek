using System;
using System.Linq;
using System.Windows.Media;
using Stylet;
using TileShop.Shared.EventModels;
using TileShop.Shared.Services;
using TileShop.WPF.Behaviors;
using TileShop.WPF.Helpers;
using TileShop.WPF.Imaging;
using TileShop.WPF.Models;
using ImageMagitek;
using ImageMagitek.Colors;
using Point = System.Drawing.Point;

namespace TileShop.WPF.ViewModels
{
    public enum PixelTool { Pencil, ColorPicker }

    public class PixelEditorViewModel : ArrangerEditorViewModel, IHandle<EditArrangerPixelsEvent>
    {
        private int _viewX;
        private int _viewY;
        private int _viewWidth;
        private int _viewHeight;
        private PencilHistoryAction _activePencilHistory;

        private BindableCollection<HistoryAction> _history = new BindableCollection<HistoryAction>();
        public BindableCollection<HistoryAction> History
        {
            get => _history;
            set => SetAndNotify(ref _history, value);
        }

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

        private bool _hasArranger;
        public bool HasArranger
        {
            get => _hasArranger;
            set => SetAndNotify(ref _hasArranger, value);
        }

        private bool _isDrawing;
        public bool IsDrawing
        {
            get => _isDrawing;
            set => SetAndNotify(ref _isDrawing, value);
        }

        private PixelTool _activeTool = PixelTool.Pencil;
        public PixelTool ActiveTool
        {
            get => _activeTool;
            set => SetAndNotify(ref _activeTool, value);
        }

        private Color _activeColor;
        public Color ActiveColor
        {
            get => _activeColor;
            set => SetAndNotify(ref _activeColor, value);
        }

        private Color _primaryColor;
        public Color PrimaryColor
        {
            get => _primaryColor;
            set => SetAndNotify(ref _primaryColor, value);
        }

        private Color _secondaryColor;
        public Color SecondaryColor
        {
            get => _secondaryColor;
            set => SetAndNotify(ref _secondaryColor, value);
        }

        public RelayCommand<Color> SetPrimaryColorCommand { get; }
        public RelayCommand<Color> SetSecondaryColorCommand { get; }

        public override bool CanShowGridlines => HasArranger;

        public PixelEditorViewModel(IEventAggregator events, IWindowManager windowManager, IPaletteService paletteService) :
            base(events, windowManager, paletteService)
        {
            _events.Subscribe(this);

            SetPrimaryColorCommand = new RelayCommand<Color>(SetPrimaryColor);
            SetSecondaryColorCommand = new RelayCommand<Color>(SetSecondaryColor);
            DisplayName = "Pixel Editor";

            Zoom = 3;
            MaxZoom = 32;
        }

        public void RemapColors()
        {
            var palette = _workingArranger.GetReferencedPalettes().FirstOrDefault();
            if (palette is null)
                palette = _paletteService.DefaultPalette;

            var colors = Math.Min(256, 1 << _workingArranger.EnumerateElements().Select(x => x.Codec?.ColorDepth ?? 0).Max());

            var remapViewModel = new ColorRemapViewModel(palette, colors);
            if(_windowManager.ShowDialog(remapViewModel) is true)
            {
                var remap = remapViewModel.FinalColors.Select(x => (byte)x.Index).ToList();
                _indexedImage.RemapColors(remap);
                Render();

                var remapAction = new ColorRemapHistoryAction(remapViewModel.InitialColors, remapViewModel.FinalColors);
                History.Add(remapAction);
                IsModified = true;
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

        protected override void Render()
        {
            if(HasArranger)
            {
                if (_workingArranger.ColorType == PixelColorType.Indexed)
                    ArrangerSource = new IndexedImageSource(_indexedImage, _workingArranger, _paletteService.DefaultPalette, _viewX, _viewY, _viewWidth, _viewHeight);
                else if (_workingArranger.ColorType == PixelColorType.Direct)
                    ArrangerSource = new DirectImageSource(_directImage, _viewX, _viewY, _viewWidth, _viewHeight);
            }
        }

        public void SetPixel(int x, int y, Color color)
        {
            var arrangerColor = new ColorRgba32(color.R, color.G, color.B, color.A);

            if (_workingArranger.ColorType == PixelColorType.Indexed)
            {
                var result = _indexedImage.TrySetPixel(x + _viewX, y + _viewY, arrangerColor);
                var notifyEvent = result.Match(
                    success =>
                    {
                        if (_activePencilHistory.ModifiedPoints.Add(new Point(x, y)))
                        {
                            IsModified = true;
                            Render();
                        }
                        return new NotifyOperationEvent("");
                    },
                    fail => new NotifyOperationEvent(fail.Reason)
                    );
                _events.PublishOnUIThread(notifyEvent);
            }
            else if (_workingArranger.ColorType == PixelColorType.Direct)
            {
                _directImage.SetPixel(x + _viewX, y + _viewY, arrangerColor);
            }
        }

        public Color GetPixel(int x, int y)
        {
            ColorRgba32 arrangerColor = new ColorRgba32(0);
            if (_workingArranger.ColorType == PixelColorType.Indexed)
            {
                arrangerColor = _indexedImage.GetPixelColor(x, y);
            }
            else if (_workingArranger.ColorType == PixelColorType.Direct)
            {
                arrangerColor = _directImage.GetPixel(x, y);
            }

            return Color.FromArgb(arrangerColor.A, arrangerColor.R, arrangerColor.G, arrangerColor.B);
        }

        public void SetPrimaryColor(Color color) => PrimaryColor = color;
        public void SetSecondaryColor(Color color) => SecondaryColor = color;

        public override void SaveChanges()
        {
            try
            {
                if (_workingArranger.ColorType == PixelColorType.Indexed)
                    _indexedImage.SaveImage();
                else if (_workingArranger.ColorType == PixelColorType.Direct)
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
            if (!HasArranger)
                return;

            if (_workingArranger.ColorType == PixelColorType.Indexed)
                _indexedImage.Render();
            else if (_workingArranger.ColorType == PixelColorType.Direct)
                _directImage.Render();

            History.Clear();
        }

        public void Reset()
        {
            History.Clear();
            HasArranger = false;
            IsModified = false;
            ArrangerSource = null;
            _workingArranger = null;
            Palettes.Clear();
            NotifyOfPropertyChange(() => CanRemapColors);
            ActivePalette = null;
            PrimaryColor = Color.FromArgb(0, 0, 0, 0);
            SecondaryColor = Color.FromArgb(0, 0, 0, 0);
        }

        public override void OnMouseDown(object sender, MouseCaptureArgs e)
        {
            int x = (int)e.X / Zoom;
            int y = (int)e.Y / Zoom;

            if(ActiveTool == PixelTool.Pencil && e.LeftButton)
            {
                _activePencilHistory = new PencilHistoryAction();
                SetPixel(x, y, PrimaryColor);
                IsDrawing = true;
            }
            else if(ActiveTool == PixelTool.Pencil && e.RightButton)
            {
                _activePencilHistory = new PencilHistoryAction();
                SetPixel(x, y, SecondaryColor);
                IsDrawing = true;
            }
            else if(ActiveTool == PixelTool.ColorPicker && e.LeftButton)
            {
                PrimaryColor = GetPixel(x, y);
                ActiveColor = PrimaryColor;
            }
            else if(ActiveTool == PixelTool.ColorPicker && e.RightButton)
            {
                SecondaryColor = GetPixel(x, y);
                ActiveColor = SecondaryColor;
            }
        }

        public override void OnMouseLeave(object sender, MouseCaptureArgs e)
        {
            if (ActiveTool == PixelTool.Pencil && IsDrawing && _activePencilHistory?.ModifiedPoints.Count > 0)
            {
                IsDrawing = false;
                History.Add(_activePencilHistory);
                _activePencilHistory = null;
            }
        }

        public override void OnMouseMove(object sender, MouseCaptureArgs e)
        {
            int x = (int)e.X / Zoom;
            int y = (int)e.Y / Zoom;

            if (x < 0 || x >= _workingArranger.ArrangerPixelSize.Width || y < 0 || y >= _workingArranger.ArrangerPixelSize.Height)
                return;

            if (IsDrawing && ActiveTool == PixelTool.Pencil && e.LeftButton)
                SetPixel(x, y, PrimaryColor);
            else if(IsDrawing && ActiveTool == PixelTool.Pencil && e.RightButton)
                SetPixel(x, y, PrimaryColor);
        }

        public override void OnMouseUp(object sender, MouseCaptureArgs e)
        {
            if(ActiveTool == PixelTool.Pencil && IsDrawing && _activePencilHistory?.ModifiedPoints.Count > 0)
            {
                IsDrawing = false;
                History.Add(_activePencilHistory);
                _activePencilHistory = null;
            }
        }

        public void Handle(EditArrangerPixelsEvent message)
        {
            if (IsModified && HasArranger && History.Count > 0)
            {
                var result = _windowManager.ShowMessageBox($"'{DisplayName}' has been modified and will be closed. Save changes?",
                    "Save changes", System.Windows.MessageBoxButton.YesNoCancel);

                if (result == System.Windows.MessageBoxResult.No)
                    History.Clear();
                else if (result == System.Windows.MessageBoxResult.Cancel)
                    return;
                else if (result == System.Windows.MessageBoxResult.Yes)
                    SaveChanges();
            }

            _workingArranger = message.ArrangerTransferModel.Arranger.CloneArranger();
            _viewX = message.ArrangerTransferModel.X;
            _viewY = message.ArrangerTransferModel.Y;
            _viewWidth = message.ArrangerTransferModel.Width;
            _viewHeight = message.ArrangerTransferModel.Height;

            History.Clear();
            Palettes.Clear();

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

            if (_workingArranger.ColorType == PixelColorType.Indexed)
            {
                _indexedImage = new IndexedImage(_workingArranger, defaultPalette);
                ArrangerSource = new IndexedImageSource(_indexedImage, _workingArranger, defaultPalette, _viewX, _viewY, _viewWidth, _viewHeight);
            }
            else if (_workingArranger.ColorType == PixelColorType.Direct)
            {
                _directImage = new DirectImage(_workingArranger);
                ArrangerSource = new DirectImageSource(_directImage);
            }

            HasArranger = true;
            DisplayName = $"Pixel Editor - {_workingArranger.Name}";

            ActivePalette = Palettes.First();
            PrimaryColor = ActivePalette.Colors[0];
            SecondaryColor = ActivePalette.Colors[1];
            NotifyOfPropertyChange(() => CanRemapColors);
        }
    }
}
