using System.Windows;
using System.Linq;
using Stylet;
using GongSolutions.Wpf.DragDrop;
using ImageMagitek;
using ImageMagitek.Colors;
using ImageMagitek.Services;
using TileShop.WPF.Imaging;
using TileShop.WPF.Models;
using TileShop.WPF.Behaviors;
using TileShop.Shared.Models;
using TileShop.Shared.EventModels;
using TileShop.WPF.ViewModels.Dialogs;
using System;

namespace TileShop.WPF.ViewModels
{
    public enum ScatteredArrangerTool { Select, ApplyPalette, PickPalette, InspectElement }

    public class ScatteredArrangerEditorViewModel : ArrangerEditorViewModel
    {
        private BindableCollection<PaletteModel> _palettes = new BindableCollection<PaletteModel>();
        public BindableCollection<PaletteModel> Palettes
        {
            get => _palettes;
            set => SetAndNotify(ref _palettes, value);
        }

        private PaletteModel _selectedPalette;
        public PaletteModel SelectedPalette
        {
            get => _selectedPalette;
            set => SetAndNotify(ref _selectedPalette, value);
        }

        private ScatteredArrangerTool _activeTool = ScatteredArrangerTool.Select;
        private readonly IProjectService _projectService;

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

        public ScatteredArrangerEditorViewModel(Arranger arranger, IEventAggregator events, IWindowManager windowManager, 
            IPaletteService paletteService, IProjectService projectService) : base(events, windowManager, paletteService)
        {
            Resource = arranger;
            _workingArranger = arranger.CloneArranger();
            DisplayName = Resource?.Name ?? "Unnamed Arranger";

            Render();
            CreateGridlines();

            if (arranger.Layout == ArrangerLayout.Single)
                SnapMode = SnapMode.Pixel;
            else if (arranger.Layout == ArrangerLayout.Tiled)
                SnapMode = SnapMode.Element;

            var palettes = _workingArranger.GetReferencedPalettes();
            palettes.ExceptWith(_paletteService.GlobalPalettes);

            var palModels = palettes.OrderBy(x => x.Name)
                .Concat(_paletteService.GlobalPalettes.OrderBy(x => x.Name))
                .Select(x => new PaletteModel(x));

            Palettes = new BindableCollection<PaletteModel>(palModels);
            SelectedPalette = Palettes.First();
            _projectService = projectService;
        }

        public void SetSelectToolMode() => ActiveTool = ScatteredArrangerTool.Select;

        public void SetApplyPaletteMode() => ActiveTool = ScatteredArrangerTool.ApplyPalette;

        public override void SaveChanges()
        {
            if (_workingArranger.Layout == ArrangerLayout.Tiled)
            {
                var treeArranger = Resource as Arranger;
                if (_workingArranger.ArrangerElementSize != treeArranger.ArrangerElementSize)
                {
                    if (treeArranger.Layout == ArrangerLayout.Tiled)
                        treeArranger.Resize(_workingArranger.ArrangerElementSize.Width, _workingArranger.ArrangerElementSize.Height);
                    else if (treeArranger.Layout == ArrangerLayout.Single)
                        treeArranger.Resize(_workingArranger.ArrangerPixelSize.Width, _workingArranger.ArrangerPixelSize.Height);
                }

                for (int y = 0; y < _workingArranger.ArrangerElementSize.Height; y++)
                {
                    for (int x = 0; x < _workingArranger.ArrangerElementSize.Width; x++)
                    {
                        var el = _workingArranger.GetElement(x, y);
                        treeArranger.SetElement(el, x, y);
                    }
                }
            }

            var projectTree = _projectService.GetContainingProject(Resource);
            _projectService.SaveProject(projectTree)
                 .Switch(
                     success => IsModified = false,
                     fail => _windowManager.ShowMessageBox($"An error occurred while saving the project tree to {projectTree.FileLocation}: {fail.Reason}")
                 );
        }

        public override void DiscardChanges()
        {
            _workingArranger = (Resource as Arranger).CloneArranger();
            Render();
            IsModified = false;
        }

        public override void OnMouseDown(object sender, MouseCaptureArgs e)
        {
            int x = (int)e.X / Zoom;
            int y = (int)e.Y / Zoom;

            if (ActiveTool == ScatteredArrangerTool.ApplyPalette && e.LeftButton)
                TryApplyPalette(x, y, SelectedPalette.Palette);
            else if (ActiveTool == ScatteredArrangerTool.PickPalette && e.LeftButton)
                TryPickPalette(x, y);
            else if (ActiveTool == ScatteredArrangerTool.Select)
                base.OnMouseDown(sender, e);
        }

        public override void OnMouseMove(object sender, MouseCaptureArgs e)
        {
            int x = Math.Clamp((int)e.X / Zoom, 0, _workingArranger.ArrangerPixelSize.Width - 1);
            int y = Math.Clamp((int)e.Y / Zoom, 0, _workingArranger.ArrangerPixelSize.Height - 1);

            if (ActiveTool == ScatteredArrangerTool.ApplyPalette && e.LeftButton)
                TryApplyPalette(x, y, SelectedPalette.Palette);
            else if (ActiveTool == ScatteredArrangerTool.InspectElement)
            {
                var elX = x / _workingArranger.ElementPixelSize.Width;
                var elY = y / _workingArranger.ElementPixelSize.Height;
                var el = _workingArranger.GetElement(elX, elY);

                string notifyMessage = $"Element ({elX}, {elY}): Palette {el.Palette?.Name ?? "Default"}, DataFile {el.DataFile?.Location ?? "None"}, FileOffset 0x{el.FileAddress.FileOffset:X}.{(el.FileAddress.BitOffset != 0 ? el.FileAddress.BitOffset.ToString() : "")}";
                var notifyEvent = new NotifyStatusEvent(notifyMessage, NotifyStatusDuration.Indefinite);
                _events.PublishOnUIThread(notifyEvent);
            }
            else if (ActiveTool == ScatteredArrangerTool.Select)
                base.OnMouseMove(sender, e);
        }

        public override void DragOver(IDropInfo dropInfo)
        {
            if (dropInfo.Data is PaletteNodeViewModel model)
            {
                var pal = model.Node.Value as Palette;
                dropInfo.DropTargetAdorner = DropTargetAdorners.Highlight;
                dropInfo.Effects = DragDropEffects.Move | DragDropEffects.Link;
                dropInfo.EffectText = $"Add palette {pal.Name}";
            }
            else
                base.DragOver(dropInfo);
        }

        public override void Drop(IDropInfo dropInfo)
        {
            if (dropInfo.Data is PaletteNodeViewModel palNodeVM)
            {
                var pal = palNodeVM.Node.Value as Palette;
                if (!Palettes.Any(x => ReferenceEquals(pal, x.Palette)))
                {
                    var palModel = new PaletteModel(pal);
                    Palettes.Add(palModel);
                    SelectedPalette = palModel;
                }
            }
            else
                base.Drop(dropInfo);
        }

        protected override void Render()
        {
            CancelOverlay();

            if (_workingArranger.ColorType == PixelColorType.Indexed)
            {
                _indexedImage = new IndexedImage(_workingArranger);
                BitmapAdapter = new IndexedBitmapAdapter(_indexedImage);
            }
            else if (_workingArranger.ColorType == PixelColorType.Direct)
            {

                _directImage = new DirectImage(_workingArranger);
                //ArrangerSource = new DirectImageSource(_directImage);
            }
        }

        private void TryApplyPalette(int pixelX, int pixelY, Palette palette)
        {
            if (pixelX >= _workingArranger.ArrangerPixelSize.Width || pixelY >= _workingArranger.ArrangerPixelSize.Height)
                return;

            var el = _workingArranger.GetElementAtPixel(pixelX, pixelY);

            if (ReferenceEquals(palette, el.Palette))
                return;

            var result = _indexedImage.TrySetPalette(pixelX, pixelY, palette);

            result.Switch(
                success =>
                {
                    Render();
                    IsModified = true;
                },
                fail => _events.PublishOnUIThread(new NotifyOperationEvent(fail.Reason))
                );
        }

        private bool TryPickPalette(int pixelX, int pixelY)
        {
            var elX = pixelX / _workingArranger.ElementPixelSize.Width;
            var elY = pixelY / _workingArranger.ElementPixelSize.Height;

            if (elX >= _workingArranger.ArrangerElementSize.Width || elY >= _workingArranger.ArrangerElementSize.Height)
                return false;

            var el = _workingArranger.GetElement(elX, elY);

            SelectedPalette = Palettes.FirstOrDefault(x => ReferenceEquals(el.Palette, x.Palette)) ??
                Palettes.First(x => ReferenceEquals(_paletteService?.DefaultPalette, x.Palette));

            return true;
        }

        public void ResizeArranger()
        {
            var model = new ResizeTiledScatteredArrangerViewModel(_windowManager, _workingArranger.ArrangerElementSize.Width, _workingArranger.ArrangerElementSize.Height);

            if (_windowManager.ShowDialog(model) is true)
            {
                _workingArranger.Resize(model.Width, model.Height);
                IsModified = true;
                Render();
            }
        }

        protected override bool CanAcceptTransfer(ArrangerTransferModel model)
        {
            CanPasteElements = CanAcceptElementTransfer(model);
            CanPastePixels = CanAcceptPixelTransfer(model);

            return CanPasteElements || CanPastePixels;
        }

        private bool CanAcceptPixelTransfer(ArrangerTransferModel model)
        {
            return true;
            //if (IsLinearLayout)
            //    return true;
            //else if (IsTiledLayout)
            //{
            //    var elems = _workingArranger.EnumerateElementsByPixel(model.X, model.Y, model.Width, model.Height);
            //    return !elems.Any(x => x.Codec is BlankIndexedCodec || x.Codec is BlankDirectCodec);
            //}

            //return false;
        }

        private bool CanAcceptElementTransfer(ArrangerTransferModel model)
        {
            // Ensure elements are an even multiple width/height
            if (model.Width % _workingArranger.ElementPixelSize.Width != 0 || model.Height % _workingArranger.ElementPixelSize.Height != 0)
                return false;

            // Ensure start point is aligned to an element boundary
            if (model.X % _workingArranger.ElementPixelSize.Width != 0 || model.Y % _workingArranger.ElementPixelSize.Height != 0)
                return false;

            // Cannot copy into a single arranger
            if (_workingArranger.Layout == ArrangerLayout.Single)
                return false;

            return true;
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
                success =>
                {
                    IsModified = true;
                    Render();
                    return new NotifyOperationEvent("Paste successfully applied");
                },
                fail => new NotifyOperationEvent(fail.Reason)
                );

            _events.PublishOnUIThread(notifyEvent);
        }

        public void DeleteSelection()
        {
            if (Overlay.State == OverlayState.Selected && _workingArranger.Layout == ArrangerLayout.Tiled)
            {
                int startX = Overlay.SelectionRect.SnappedLeft / _workingArranger.ElementPixelSize.Width;
                int startY = Overlay.SelectionRect.SnappedTop / _workingArranger.ElementPixelSize.Height;
                int width = Overlay.SelectionRect.SnappedWidth / _workingArranger.ElementPixelSize.Height;
                int height = Overlay.SelectionRect.SnappedHeight / _workingArranger.ElementPixelSize.Width;

                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        _workingArranger.ResetElement(x + startX, y + startY);
                    }
                }

                IsModified = true;
                Render();
            }
        }
    }
}
