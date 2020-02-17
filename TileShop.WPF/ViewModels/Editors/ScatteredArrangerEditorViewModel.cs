using Stylet;
using ImageMagitek;
using System;
using TileShop.WPF.Helpers;
using GongSolutions.Wpf.DragDrop;
using TileShop.WPF.Imaging;
using ImageMagitek.Colors;
using TileShop.Shared.Services;
using TileShop.WPF.Models;
using System.Linq;
using TileShop.WPF.Behaviors;

namespace TileShop.WPF.ViewModels
{
    public enum EditMode { ArrangeGraphics, ModifyGraphics }

    public enum ScatteredArrangerTool { Select, ApplyPalette, PickPalette }

    public class ScatteredArrangerEditorViewModel : ArrangerEditorViewModel, IDropTarget
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

        public ScatteredArrangerEditorViewModel(Arranger arranger, IEventAggregator events) : this(arranger, events, null) { }

        public ScatteredArrangerEditorViewModel(Arranger arranger, IEventAggregator events, IPaletteService paletteService)
        {
            Resource = arranger;
            _arranger = arranger;
            _events = events;
            _paletteService = paletteService;
            _defaultPalette = _paletteService?.DefaultPalette;

            RenderArranger();
            CreateGridlines();

            if (arranger.Layout == ArrangerLayout.Tiled)
                Selection = new ArrangerSelector(_arranger.ArrangerPixelSize, _arranger.ElementPixelSize, SnapMode.Element);
            else
                Selection = new ArrangerSelector(_arranger.ArrangerPixelSize, _arranger.ElementPixelSize, SnapMode.Pixel);

            var arrangerPalettes = _arranger.GetReferencedPalettes().OrderBy(x => x.Name).ToList();
            arrangerPalettes.Add(_defaultPalette);
            Palettes = new BindableCollection<PaletteModel>(arrangerPalettes.Select(x => new PaletteModel(x)));
            ActivePalette = Palettes.First();
        }

        public void SetSelectToolMode() => ActiveTool = ScatteredArrangerTool.Select;

        public void SetApplyPaletteMode() => ActiveTool = ScatteredArrangerTool.ApplyPalette;

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

            if (ActiveTool == ScatteredArrangerTool.ApplyPalette && e.LeftButton)
                ApplyPalette(x, y, ActivePalette.Palette);
            else if (ActiveTool == ScatteredArrangerTool.PickPalette && e.LeftButton)
                PickPalette(x, y);
            else
                base.OnMouseDown(sender, e);
        }

        public override void OnMouseMove(object sender, MouseCaptureArgs e)
        {
            int x = (int)e.X / Zoom;
            int y = (int)e.Y / Zoom;

            if (ActiveTool == ScatteredArrangerTool.ApplyPalette && e.LeftButton)
                ApplyPalette(x, y, ActivePalette.Palette);
            else
                base.OnMouseMove(sender, e);
        }

        public void DragOver(IDropInfo dropInfo)
        {
            throw new NotImplementedException();
        }

        public void Drop(IDropInfo dropInfo)
        {
            throw new NotImplementedException();
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

        private void ApplyPalette(int pixelX, int pixelY, Palette palette)
        {
            var elX = pixelX / _arranger.ElementPixelSize.Width;
            var elY = pixelY / _arranger.ElementPixelSize.Height;

            if (elX >= _arranger.ArrangerElementSize.Width || elY >= _arranger.ArrangerElementSize.Height)
                return;

            var el = _arranger.GetElement(elX, elY);

            if (ReferenceEquals(palette, el.Palette))
                return;

            if (ReferenceEquals(_defaultPalette, palette))
                el = el.WithPalette(null);
            else
                el = el.WithPalette(palette);

            _arranger.SetElement(el, elX, elY);
            RenderArranger();
        }

        private void PickPalette(int pixelX, int pixelY)
        {
            var elX = pixelX / _arranger.ElementPixelSize.Width;
            var elY = pixelY / _arranger.ElementPixelSize.Height;

            if (elX >= _arranger.ArrangerElementSize.Width || elY >= _arranger.ArrangerElementSize.Height)
                return;

            var el = _arranger.GetElement(elX, elY);

            ActivePalette = Palettes.FirstOrDefault(x => ReferenceEquals(el.Palette, x.Palette)) ?? Palettes.First(x => ReferenceEquals(_defaultPalette, x.Palette));
        }
    }
}
