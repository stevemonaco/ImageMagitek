using Caliburn.Micro;
using ImageMagitek;
using ImageMagitek.Colors;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Shapes;
using TileShop.Shared.EventModels;
using TileShop.Shared.Services;
using TileShop.WPF.Behaviors;
using TileShop.WPF.Helpers;
using TileShop.WPF.Imaging;
using TileShop.WPF.Models;
using TileShop.WPF.Services;
using Point = System.Drawing.Point;

namespace TileShop.WPF.ViewModels
{
    public enum PixelTool { Pencil, ColorPicker }

    public class PixelEditorViewModel : ArrangerEditorViewModel, IMouseCaptureProxy, IHandle<EditArrangerPixelsEvent>
    {
        private IUserPromptService _promptService;
        private IPaletteService _paletteService;
        private int _viewX;
        private int _viewY;
        private int _viewWidth;
        private int _viewHeight;
        private PencilHistoryAction _activePencilHistory;

        public override string Name => HasArranger ? $"Pixel Editor - {_arranger.Name}" : "Pixel Editor";

        private BindableCollection<HistoryAction> _history = new BindableCollection<HistoryAction>();
        public BindableCollection<HistoryAction> History
        {
            get => _history;
            set => Set(ref _history, value);
        }

        private BindableCollection<PaletteModel> _palettes = new BindableCollection<PaletteModel>();
        public BindableCollection<PaletteModel> Palettes
        {
            get => _palettes;
            set => Set(ref _palettes, value);
        }

        private PaletteModel _activePalette;
        public PaletteModel ActivePalette
        {
            get => _activePalette;
            set => Set(ref _activePalette, value);
        }

        private bool _hasArranger;
        public bool HasArranger
        {
            get => _hasArranger;
            set => Set(ref _hasArranger, value);
        }

        private bool _isDrawing;
        public bool IsDrawing
        {
            get => _isDrawing;
            set => Set(ref _isDrawing, value);
        }

        private PixelTool _activeTool = PixelTool.Pencil;
        public PixelTool ActiveTool
        {
            get => _activeTool;
            set => Set(ref _activeTool, value);
        }

        private Color _activeColor;
        public Color ActiveColor
        {
            get => _activeColor;
            set => Set(ref _activeColor, value);
        }

        private Color _primaryColor;
        public Color PrimaryColor
        {
            get => _primaryColor;
            set => Set(ref _primaryColor, value);
        }

        private Color _secondaryColor;
        public Color SecondaryColor
        {
            get => _secondaryColor;
            set => Set(ref _secondaryColor, value);
        }

        public RelayCommand<Color> SetPrimaryColorCommand { get; }
        public RelayCommand<Color> SetSecondaryColorCommand { get; }

        public override bool CanShowGridlines => HasArranger;

        public PixelEditorViewModel(IEventAggregator events, IUserPromptService promptService, IPaletteService paletteService)
        {
            _events = events;
            _promptService = promptService;
            _paletteService = paletteService;

            _events.SubscribeOnUIThread(this);

            SetPrimaryColorCommand = new RelayCommand<Color>(SetPrimaryColor);
            SetSecondaryColorCommand = new RelayCommand<Color>(SetSecondaryColor);
        }

        public void RemapColors()
        {

        }

        public bool CanRemapColors
        {
            get
            {
                var palettes = _arranger?.GetReferencedPalettes();
                if (palettes?.Count <= 1)
                    return _arranger.GetReferencedCodecs().All(x => x.ColorType == PixelColorType.Indexed);

                return false;
            }
        }

        public void Render()
        {
            if(HasArranger)
            {
                if (_arranger.ColorType == PixelColorType.Indexed)
                    ArrangerSource = new IndexedImageSource(_indexedImage, _arranger, _paletteService.DefaultPalette, _viewX, _viewY, _viewWidth, _viewHeight);
                else if (_arranger.ColorType == PixelColorType.Direct)
                    ArrangerSource = new DirectImageSource(_directImage, _viewX, _viewY, _viewWidth, _viewHeight);
            }
        }

        public bool SetPixel(int x, int y, Color color)
        {
            var arrangerColor = new ColorRgba32(color.R, color.G, color.B, color.A);

            if (_arranger.ColorType == PixelColorType.Indexed)
            {
                if (_indexedImage.TrySetPixel(x + _viewX, y + _viewY, arrangerColor))
                {
                    Render();
                    return true;
                }
            }
            else if (_arranger.ColorType == PixelColorType.Direct)
            {
                _directImage.SetPixel(x + _viewX, y + _viewY, arrangerColor);
                return true;
            }

            return false;
        }

        public Color GetPixel(int x, int y)
        {
            ColorRgba32 arrangerColor = new ColorRgba32(0);
            if (_arranger.ColorType == PixelColorType.Indexed)
            {
                arrangerColor = _indexedImage.GetPixel(x, y, _arranger);
            }
            else if (_arranger.ColorType == PixelColorType.Direct)
            {
                arrangerColor = _directImage.GetPixel(x, y);
            }

            return Color.FromArgb(arrangerColor.A, arrangerColor.R, arrangerColor.G, arrangerColor.B);
        }

        public void SetPrimaryColor(Color color) => PrimaryColor = color;
        public void SetSecondaryColor(Color color) => SecondaryColor = color;

        public override bool SaveChanges()
        {
            return true;
        }

        public override bool DiscardChanges()
        {
            return true;
        }

        public override void OnMouseDown(object sender, MouseCaptureArgs e)
        {
            int x = (int)e.X / Zoom;
            int y = (int)e.Y / Zoom;

            if(ActiveTool == PixelTool.Pencil && e.LeftButton)
            {
                SetPixel(x, y, PrimaryColor);
                _activePencilHistory = new PencilHistoryAction();
                _activePencilHistory.ModifiedPoints.Add(new Point(x, y));
                IsDrawing = true;
            }
            else if(ActiveTool == PixelTool.Pencil && e.RightButton)
            {
                SetPixel(x, y, SecondaryColor);
                _activePencilHistory = new PencilHistoryAction();
                _activePencilHistory.ModifiedPoints.Add(new Point(x, y));
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

            if (x < 0 || x >= _arranger.ArrangerPixelSize.Width || y < 0 || y >= _arranger.ArrangerPixelSize.Height)
                return;

            if (IsDrawing && ActiveTool == PixelTool.Pencil && e.LeftButton)
            {
                if(_activePencilHistory.Add(x, y))
                {
                    SetPixel(x, y, PrimaryColor);
                }
            }
            else if(IsDrawing && ActiveTool == PixelTool.Pencil && e.RightButton)
            {
                if (_activePencilHistory.Add(e.X, e.Y))
                {
                    SetPixel(x, y, SecondaryColor);
                }
            }
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

        public Task HandleAsync(EditArrangerPixelsEvent message, CancellationToken cancellationToken)
        {
            if (IsModified && HasArranger)
            {
                var result = _promptService.PromptUser($"{_arranger.Name} has been modified and will be closed. Would you like to save the changes?",
                    "Save changes", UserPromptChoices.YesNoCancel);

                if (result == UserPromptResult.Cancel)
                    return Task.CompletedTask;
                else if (result == UserPromptResult.Yes)
                {
                    // Save pixel data to arranger
                }
            }

            _arranger = message.ArrangerTransferModel.Arranger;
            _viewX = message.ArrangerTransferModel.X;
            _viewY = message.ArrangerTransferModel.Y;
            _viewWidth = message.ArrangerTransferModel.Width;
            _viewHeight = message.ArrangerTransferModel.Height;

            Palettes.Clear();

            var arrangerPalettes = _arranger.GetReferencedPalettes().OrderBy(x => x.Name);

            foreach (var pal in arrangerPalettes)
                Palettes.Add(PaletteModel.FromArrangerPalette(pal));

            var defaultPalette = _paletteService.DefaultPalette;
            Palettes.Add(PaletteModel.FromArrangerPalette(defaultPalette));

            if (_arranger.ColorType == PixelColorType.Indexed)
            {
                _indexedImage = new IndexedImage(_arranger, defaultPalette);
                ArrangerSource = new IndexedImageSource(_indexedImage, _arranger, defaultPalette, _viewX, _viewY, _viewWidth, _viewHeight);
            }
            else if (_arranger.ColorType == PixelColorType.Direct)
            {
                _directImage = new DirectImage(_arranger);
                ArrangerSource = new DirectImageSource(_directImage);
            }

            HasArranger = true;

            ActivePalette = Palettes.First();
            PrimaryColor = ActivePalette.Colors[0];
            SecondaryColor = ActivePalette.Colors[1];

            NotifyOfPropertyChange(() => CanRemapColors);

            return Task.CompletedTask;
        }
    }
}
