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
using Monaco.PathTree;
using Point = System.Drawing.Point;

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
        private ApplyPaletteHistoryAction _applyPaletteHistory;
        private readonly IProjectService _projectService;
        private IndexedImage _indexedImage;
        private DirectImage _directImage;

        public ScatteredArrangerTool ActiveTool
        {
            get => _activeTool;
            set
            {
                if (value != ScatteredArrangerTool.Select && value != ScatteredArrangerTool.ApplyPalette)
                    CancelOverlay();
                SetAndNotify(ref _activeTool, value);
            }
        }

        public ScatteredArrangerEditorViewModel(Arranger arranger, IEventAggregator events, IWindowManager windowManager,
            IPaletteService paletteService, IProjectService projectService) : base(events, windowManager, paletteService)
        {
            Resource = arranger;
            WorkingArranger = arranger.CloneArranger();
            DisplayName = Resource?.Name ?? "Unnamed Arranger";

            CreateImages();

            if (arranger.Layout == ArrangerLayout.Single)
            {
                SnapMode = SnapMode.Pixel;
            }
            else if (arranger.Layout == ArrangerLayout.Tiled)
            {
                SnapMode = SnapMode.Element;
                CanChangeSnapMode = true;
                CanAcceptElementPastes = true;
            }

            var palettes = WorkingArranger.GetReferencedPalettes();
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
            if (WorkingArranger.Layout == ArrangerLayout.Tiled)
            {
                var treeArranger = Resource as Arranger;
                if (WorkingArranger.ArrangerElementSize != treeArranger.ArrangerElementSize)
                {
                    if (treeArranger.Layout == ArrangerLayout.Tiled)
                        treeArranger.Resize(WorkingArranger.ArrangerElementSize.Width, WorkingArranger.ArrangerElementSize.Height);
                    else if (treeArranger.Layout == ArrangerLayout.Single)
                        treeArranger.Resize(WorkingArranger.ArrangerPixelSize.Width, WorkingArranger.ArrangerPixelSize.Height);
                }

                for (int y = 0; y < WorkingArranger.ArrangerElementSize.Height; y++)
                {
                    for (int x = 0; x < WorkingArranger.ArrangerElementSize.Width; x++)
                    {
                        var el = WorkingArranger.GetElement(x, y);
                        treeArranger.SetElement(el, x, y);
                    }
                }
            }

            var projectTree = _projectService.GetContainingProject(Resource);
            _projectService.SaveProject(projectTree)
                 .Switch(
                     success =>
                     {
                         UndoHistory.Clear();
                         RedoHistory.Clear();
                         NotifyOfPropertyChange(() => CanUndo);
                         NotifyOfPropertyChange(() => CanRedo);

                         IsModified = false;
                     },
                     fail => _windowManager.ShowMessageBox($"An error occurred while saving the project tree to {projectTree.FileLocation}: {fail.Reason}")
                 );
        }

        public override void DiscardChanges()
        {
            WorkingArranger = (Resource as Arranger).CloneArranger();
            CreateImages();
            IsModified = false;
        }

        #region Mouse Actions
        public override void OnMouseDown(object sender, MouseCaptureArgs e)
        {
            int x = Math.Clamp((int)e.X / Zoom, 0, WorkingArranger.ArrangerPixelSize.Width - 1);
            int y = Math.Clamp((int)e.Y / Zoom, 0, WorkingArranger.ArrangerPixelSize.Height - 1);

            if (ActiveTool == ScatteredArrangerTool.ApplyPalette && e.LeftButton)
            {
                _applyPaletteHistory = new ApplyPaletteHistoryAction(SelectedPalette.Palette);
                TryApplyPalette(x, y, SelectedPalette.Palette);
            }
            else if (ActiveTool == ScatteredArrangerTool.PickPalette && e.LeftButton)
                TryPickPalette(x, y);
            else if (ActiveTool == ScatteredArrangerTool.Select)
                base.OnMouseDown(sender, e);
        }

        public override void OnMouseUp(object sender, MouseCaptureArgs e)
        {
            if (ActiveTool == ScatteredArrangerTool.ApplyPalette && _applyPaletteHistory?.ModifiedElements.Count > 0)
            {
                AddHistoryAction(_applyPaletteHistory);
                _applyPaletteHistory = null;
            }
            else
                base.OnMouseUp(sender, e);
        }

        public override void OnMouseLeave(object sender, MouseCaptureArgs e)
        {
            if (ActiveTool == ScatteredArrangerTool.ApplyPalette && _applyPaletteHistory?.ModifiedElements.Count > 0)
            {
                AddHistoryAction(_applyPaletteHistory);
                _applyPaletteHistory = null;
            }
            else
                base.OnMouseLeave(sender, e);
        }

        public override void OnMouseMove(object sender, MouseCaptureArgs e)
        {
            int x = Math.Clamp((int)e.X / Zoom, 0, WorkingArranger.ArrangerPixelSize.Width - 1);
            int y = Math.Clamp((int)e.Y / Zoom, 0, WorkingArranger.ArrangerPixelSize.Height - 1);

            if (ActiveTool == ScatteredArrangerTool.ApplyPalette && e.LeftButton)
            {
                TryApplyPalette(x, y, SelectedPalette.Palette);
            }
            else if (ActiveTool == ScatteredArrangerTool.InspectElement)
            {
                var elX = x / WorkingArranger.ElementPixelSize.Width;
                var elY = y / WorkingArranger.ElementPixelSize.Height;
                var el = WorkingArranger.GetElement(elX, elY);

                if (el is ArrangerElement element)
                {
                    string notifyMessage = $"Element ({elX}, {elY}): Palette {element.Palette?.Name ?? "Default"}, DataFile {element.DataFile?.Location ?? "None"}, FileOffset 0x{element.FileAddress.FileOffset:X}.{(element.FileAddress.BitOffset != 0 ? element.FileAddress.BitOffset.ToString() : "")}";
                    var notifyEvent = new NotifyStatusEvent(notifyMessage, NotifyStatusDuration.Indefinite);
                    _events.PublishOnUIThread(notifyEvent);
                }
            }
            else if (ActiveTool == ScatteredArrangerTool.Select)
            {
                base.OnMouseMove(sender, e);
            }
        }
        #endregion

        #region Drag and Drop Overrides
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
                if (!_projectService.AreResourcesInSameProject(OriginatingProjectResource, palNodeVM.Node.Resource))
                {
                    var notifyEvent = new NotifyOperationEvent("Copying palettes across projects is not permitted");
                    _events.PublishOnUIThread(notifyEvent);
                    return;
                }

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
        #endregion

        private void CreateImages()
        {
            CancelOverlay();

            if (WorkingArranger.ColorType == PixelColorType.Indexed)
            {
                _indexedImage = new IndexedImage(WorkingArranger);
                BitmapAdapter = new IndexedBitmapAdapter(_indexedImage);
            }
            else if (WorkingArranger.ColorType == PixelColorType.Direct)
            {
                _directImage = new DirectImage(WorkingArranger);
                BitmapAdapter = new DirectBitmapAdapter(_directImage);
            }

            CreateGridlines();
        }

        protected override void CreateGridlines()
        {
            if (WorkingArranger.Layout == ArrangerLayout.Single)
            {
                CreateGridlines(0, 0, WorkingArranger.ArrangerPixelSize.Width, WorkingArranger.ArrangerPixelSize.Height, 8, 8);
            }
            else if (WorkingArranger.Layout == ArrangerLayout.Tiled)
            {
                base.CreateGridlines();
            }
        }

        public override void Render()
        {
            CancelOverlay();

            if (WorkingArranger.ColorType == PixelColorType.Indexed)
            {
                _indexedImage.Render();
                BitmapAdapter.Invalidate();
            }
            else if (WorkingArranger.ColorType == PixelColorType.Direct)
            {

                _directImage.Render();
                BitmapAdapter.Invalidate();
            }
        }

        private MagitekResult ApplyPasteInternal(ArrangerPaste paste)
        {
            if (paste?.Copy is not ElementCopy elementCopy)
                return new MagitekResult.Failed("No valid Paste selection");

            if (!_projectService.AreResourcesInSameProject(elementCopy.ProjectResource, OriginatingProjectResource))
                return new MagitekResult.Failed("Copying arranger elements across projects is not permitted");

            var sourceArranger = paste.Copy.Source;
            var destRect = paste.Rect;

            var destElemWidth = WorkingArranger.ElementPixelSize.Width;
            var destElemHeight = WorkingArranger.ElementPixelSize.Height;

            int destX = Math.Max(0, destRect.SnappedLeft / destElemWidth);
            int destY = Math.Max(0, destRect.SnappedTop / destElemHeight);
            int sourceX = destRect.SnappedLeft / destElemWidth >= 0 ? 0 : -destRect.SnappedLeft / destElemWidth;
            int sourceY = destRect.SnappedTop / destElemHeight >= 0 ? 0 : -destRect.SnappedTop / destElemHeight;

            var destStart = new Point(destX, destY);
            var sourceStart = new Point(sourceX, sourceY);

            int copyWidth = Math.Min(elementCopy.Width - sourceX, WorkingArranger.ArrangerElementSize.Width - destX);
            int copyHeight = Math.Min(elementCopy.Height - sourceY, WorkingArranger.ArrangerElementSize.Height - destY);

            return ElementCopier.CopyElements(elementCopy, WorkingArranger as ScatteredArranger, sourceStart, destStart, copyWidth, copyHeight);
        }

        #region Commands
        private void TryApplyPalette(int pixelX, int pixelY, Palette palette)
        {
            bool needsRender = false;
            if (Selection.HasSelection && Selection.SelectionRect.ContainsPointSnapped(pixelX, pixelY))
            {
                int top = Selection.SelectionRect.SnappedTop / WorkingArranger.ElementPixelSize.Height;
                int bottom = Selection.SelectionRect.SnappedBottom / WorkingArranger.ElementPixelSize.Height;
                int left = Selection.SelectionRect.SnappedLeft / WorkingArranger.ElementPixelSize.Width;
                int right = Selection.SelectionRect.SnappedRight / WorkingArranger.ElementPixelSize.Width;

                for (int posY = top; posY < bottom; posY++)
                {
                    for (int posX = left; posX < right; posX++)
                    {
                        int applyPixelX = posX * WorkingArranger.ElementPixelSize.Width;
                        int applyPixelY = posY * WorkingArranger.ElementPixelSize.Height;
                        if (TryApplySinglePalette(applyPixelX, applyPixelY, SelectedPalette.Palette, false))
                        {
                            needsRender = true;
                        }
                    }
                }
            }
            else
            {
                if (TryApplySinglePalette(pixelX, pixelY, palette, true))
                    needsRender = true;
            }

            if (needsRender)
                Render();

            bool TryApplySinglePalette(int pixelX, int pixelY, Palette palette, bool notify)
            {
                if (pixelX >= WorkingArranger.ArrangerPixelSize.Width || pixelY >= WorkingArranger.ArrangerPixelSize.Height)
                    return false;

                var el = WorkingArranger.GetElementAtPixel(pixelX, pixelY);

                if (el is ArrangerElement element)
                {
                    if (ReferenceEquals(palette, element.Palette))
                        return false;

                    var result = _indexedImage.TrySetPalette(pixelX, pixelY, palette);

                    return result.Match(
                        success =>
                        {
                            _applyPaletteHistory.Add(pixelX, pixelY);
                            Render();
                            IsModified = true;
                            return true;
                        },
                        fail =>
                        {
                            if (notify)
                                _events.PublishOnUIThread(new NotifyOperationEvent(fail.Reason));
                            return false;
                        });
                }
                else
                    return false;
            }
        }

        private bool TryPickPalette(int pixelX, int pixelY)
        {
            var elX = pixelX / WorkingArranger.ElementPixelSize.Width;
            var elY = pixelY / WorkingArranger.ElementPixelSize.Height;

            if (elX >= WorkingArranger.ArrangerElementSize.Width || elY >= WorkingArranger.ArrangerElementSize.Height)
                return false;

            var el = WorkingArranger.GetElement(elX, elY);

            if (el is ArrangerElement element)
            {
                SelectedPalette = Palettes.FirstOrDefault(x => ReferenceEquals(element.Palette, x.Palette)) ??
                    Palettes.First(x => ReferenceEquals(_paletteService?.DefaultPalette, x.Palette));
            }

            return true;
        }

        public void ResizeArranger()
        {
            var model = new ResizeTiledScatteredArrangerViewModel(_windowManager, WorkingArranger.ArrangerElementSize.Width, WorkingArranger.ArrangerElementSize.Height);

            if (_windowManager.ShowDialog(model) is true)
            {
                WorkingArranger.Resize(model.Width, model.Height);
                CreateImages();
                AddHistoryAction(new ResizeArrangerHistoryAction(model.Width, model.Height));

                IsModified = true;
            }
        }

        public void AssociatePalette()
        {
            var projectTree = _projectService.GetContainingProject(Resource);
            var palettes = projectTree.Tree.EnumerateDepthFirst()
                .Where(x => x.Value is Palette)
                .Select(x => new AssociatePaletteModel(x.Value as Palette, x.PathKey))
                .Concat(_projectService.GlobalResources.OfType<Palette>().Select(x => new AssociatePaletteModel(x, x.Name)));

            var model = new AssociatePaletteViewModel(palettes);

            if (_windowManager.ShowDialog(model) is true)
            {
                var palModel = new PaletteModel(model.SelectedPalette.Palette, model.SelectedPalette.Palette.Entries);
                Palettes.Add(palModel);
            }
        }

        public void ToggleSnapMode()
        {
            if (SnapMode == SnapMode.Element)
                SnapMode = SnapMode.Pixel;
            else if (SnapMode == SnapMode.Pixel)
                SnapMode = SnapMode.Element;
        }

        public void ConfirmPendingOperation()
        {
            if (Paste?.Copy is ElementCopy)
                ApplyPaste(Paste);
        }

        /// <summary>
        /// Applies the paste as elements
        /// </summary>
        public override void ApplyPaste(ArrangerPaste paste)
        {
            var notifyEvent = ApplyPasteInternal(paste).Match(
                success =>
                {
                    AddHistoryAction(new PasteArrangerHistoryAction(paste));
                    IsModified = true;
                    Render();
                    return new NotifyOperationEvent("Paste successfully applied");
                },
                fail => new NotifyOperationEvent(fail.Reason)
                );

            _events.PublishOnUIThread(notifyEvent);
        }

        public void DeleteElementSelection()
        {
            if (Selection.HasSelection)
            {
                DeleteElementSelection(Selection.SelectionRect);
                AddHistoryAction(new DeleteElementSelectionHistoryAction(Selection.SelectionRect));

                IsModified = true;
                Render();
            }
        }

        private void DeleteElementSelection(SnappedRectangle rect)
        {
            int startX = rect.SnappedLeft / WorkingArranger.ElementPixelSize.Width;
            int startY = rect.SnappedTop / WorkingArranger.ElementPixelSize.Height;
            int width = rect.SnappedWidth / WorkingArranger.ElementPixelSize.Height;
            int height = rect.SnappedHeight / WorkingArranger.ElementPixelSize.Width;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    WorkingArranger.ResetElement(x + startX, y + startY);
                }
            }
        }
        #endregion

        #region Undo Redo Actions
        public override void ApplyHistoryAction(HistoryAction action)
        {
            if (action is PasteArrangerHistoryAction pasteAction)
            {
                ApplyPasteInternal(pasteAction.Paste);
                //int x = pasteAction.Paste.Rect.SnappedLeft / _workingArranger.ElementPixelSize.Width;
                //int y = pasteAction.Paste.Rect.SnappedTop / _workingArranger.ElementPixelSize.Height;
                //ElementCopier.CopyElements(pasteAction.Paste.Copy as ElementCopy, _workingArranger as ScatteredArranger, new Point(x, y));
            }
            else if (action is DeleteElementSelectionHistoryAction deleteSelectionAction)
            {
                DeleteElementSelection(deleteSelectionAction.Rect);
            }
            else if (action is ApplyPaletteHistoryAction applyPaletteAction)
            {
                foreach (var location in applyPaletteAction.ModifiedElements)
                {
                    _indexedImage.TrySetPalette(location.X, location.Y, applyPaletteAction.Palette);
                }
            }
            else if (action is ResizeArrangerHistoryAction resizeAction)
            {
                WorkingArranger.Resize(resizeAction.Width, resizeAction.Height);
                CreateImages();
            }
        }

        public override void Undo()
        {
            if (!CanUndo)
                return;

            var lastAction = UndoHistory[^1];
            UndoHistory.RemoveAt(UndoHistory.Count - 1);
            RedoHistory.Add(lastAction);
            NotifyOfPropertyChange(() => CanUndo);
            NotifyOfPropertyChange(() => CanRedo);

            IsModified = UndoHistory.Count > 0;

            WorkingArranger = (Resource as Arranger).CloneArranger();
            CreateImages();

            foreach (var action in UndoHistory)
                ApplyHistoryAction(action);

            Render();
        }

        public override void Redo()
        {
            if (!CanRedo)
                return;

            var redoAction = RedoHistory[^1];
            RedoHistory.RemoveAt(RedoHistory.Count - 1);
            UndoHistory.Add(redoAction);
            NotifyOfPropertyChange(() => CanUndo);
            NotifyOfPropertyChange(() => CanRedo);

            ApplyHistoryAction(redoAction);
            IsModified = true;
            Render();
        }
        #endregion
    }
}
