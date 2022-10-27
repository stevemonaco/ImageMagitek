﻿using System;
using System.Linq;
using System.Collections.ObjectModel;
using ImageMagitek;
using ImageMagitek.Colors;
using ImageMagitek.Services;
using TileShop.Shared.Models;
using ImageMagitek.ExtensionMethods;
using TileShop.AvaloniaUI.ViewExtenders;
using TileShop.AvaloniaUI.Imaging;
using TileShop.AvaloniaUI.Models;
using CommunityToolkit.Mvvm.ComponentModel;

using Point = System.Drawing.Point;
using CommunityToolkit.Mvvm.Input;
using Monaco.PathTree;
using TileShop.Shared.EventModels;
using CommunityToolkit.Mvvm.Messaging;
using TileShop.Shared.Input;
using TileShop.Shared.Dialogs;

namespace TileShop.AvaloniaUI.ViewModels;

public enum ScatteredArrangerTool { Select, ApplyPalette, PickPalette, InspectElement, RotateLeft, RotateRight, MirrorHorizontal, MirrorVertical }

public partial class ScatteredArrangerEditorViewModel : ArrangerEditorViewModel
{
    [ObservableProperty] private ObservableCollection<PaletteModel> _palettes = new();
    [ObservableProperty] private PaletteModel _selectedPalette;
    [ObservableProperty] private bool _areSymmetryToolsEnabled;

    private ScatteredArrangerTool _activeTool = ScatteredArrangerTool.Select;
    private ApplyPaletteHistoryAction? _applyPaletteHistory;
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
            SetProperty(ref _activeTool, value);
        }
    }

    public ScatteredArrangerEditorViewModel(Arranger arranger, IWindowManager windowManager,
        IPaletteService paletteService, IProjectService projectService, AppSettings settings) :
        base(windowManager, paletteService)
    {
        Resource = arranger;
        WorkingArranger = arranger.CloneArranger();
        DisplayName = Resource?.Name ?? "Unnamed Arranger";
        AreSymmetryToolsEnabled = settings.EnableArrangerSymmetryTools;

        CreateImages();

        if (arranger.Layout == ElementLayout.Single)
        {
            SnapMode = SnapMode.Pixel;
        }
        else if (arranger.Layout == ElementLayout.Tiled)
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

        Palettes = new ObservableCollection<PaletteModel>(palModels);
        SelectedPalette = Palettes.First();
        _projectService = projectService;
    }

    public void SetSelectToolMode() => ActiveTool = ScatteredArrangerTool.Select;

    public void SetApplyPaletteMode() => ActiveTool = ScatteredArrangerTool.ApplyPalette;

    [RelayCommand]
    public override void SaveChanges()
    {
        if (WorkingArranger.Layout == ElementLayout.Tiled)
        {
            var treeArranger = (Arranger) Resource;
            if (WorkingArranger.ArrangerElementSize != treeArranger.ArrangerElementSize)
            {
                if (treeArranger.Layout == ElementLayout.Tiled)
                    treeArranger.Resize(WorkingArranger.ArrangerElementSize.Width, WorkingArranger.ArrangerElementSize.Height);
                else if (treeArranger.Layout == ElementLayout.Single)
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
        projectTree.TryFindResourceNode(Resource, out var resourceNode);

        _projectService.SaveResource(projectTree, resourceNode, true)
             .Switch(
                 success =>
                 {
                     UndoHistory.Clear();
                     RedoHistory.Clear();
                     OnPropertyChanged(nameof(CanUndo));
                     OnPropertyChanged(nameof(CanRedo));

                     IsModified = false;
                 },
                 fail => _windowManager.ShowMessageBox("", $"An error occurred while saving the project tree to {projectTree.Root.DiskLocation}: {fail.Reason}")
             );
    }

    public override void DiscardChanges()
    {
        WorkingArranger = ((Arranger)Resource).CloneArranger();
        CreateImages();
        IsModified = false;
    }

    #region Mouse Actions
    public override void MouseDown(double x, double y, MouseState mouseState)
    {
        int xc = Math.Clamp((int)x, 0, WorkingArranger.ArrangerPixelSize.Width - 1);
        int yc = Math.Clamp((int)y, 0, WorkingArranger.ArrangerPixelSize.Height - 1);
        var elementX = xc / WorkingArranger.ElementPixelSize.Width;
        var elementY = yc / WorkingArranger.ElementPixelSize.Height;

        if (ActiveTool == ScatteredArrangerTool.ApplyPalette && mouseState.LeftButtonPressed)
        {
            _applyPaletteHistory = new ApplyPaletteHistoryAction(SelectedPalette.Palette);
            TryApplyPalette(xc, yc, SelectedPalette.Palette);
        }
        else if (ActiveTool == ScatteredArrangerTool.PickPalette && mouseState.LeftButtonPressed)
        {
            TryPickPalette(xc, yc);
        }
        else if (ActiveTool == ScatteredArrangerTool.RotateLeft && mouseState.LeftButtonPressed)
        {
            var result = WorkingArranger.TryRotateElement(elementX, elementY, RotationOperation.Left);
            if (result.HasSucceeded)
            {
                AddHistoryAction(new RotateElementHistoryAction(elementX, elementY, RotationOperation.Left));
                IsModified = true;
                Render();
            }
        }
        else if (ActiveTool == ScatteredArrangerTool.RotateRight && mouseState.LeftButtonPressed)
        {
            var result = WorkingArranger.TryRotateElement(elementX, elementY, RotationOperation.Right);
            if (result.HasSucceeded)
            {
                AddHistoryAction(new RotateElementHistoryAction(elementX, elementY, RotationOperation.Right));
                IsModified = true;
                Render();
            }
        }
        else if (ActiveTool == ScatteredArrangerTool.MirrorHorizontal && mouseState.LeftButtonPressed)
        {
            var result = WorkingArranger.TryMirrorElement(elementX, elementY, MirrorOperation.Horizontal);
            if (result.HasSucceeded)
            {
                AddHistoryAction(new MirrorElementHistoryAction(elementX, elementY, MirrorOperation.Horizontal));
                IsModified = true;
                Render();
            }
        }
        else if (ActiveTool == ScatteredArrangerTool.MirrorVertical && mouseState.LeftButtonPressed)
        {
            var result = WorkingArranger.TryMirrorElement(elementX, elementY, MirrorOperation.Vertical);
            if (result.HasSucceeded)
            {
                AddHistoryAction(new MirrorElementHistoryAction(elementX, elementY, MirrorOperation.Vertical));
                IsModified = true;
                Render();
            }
        }
        else if (ActiveTool == ScatteredArrangerTool.Select)
        {
            base.MouseDown(x, y, mouseState);
        }
    }

    public override void MouseUp(double x, double y, MouseState mouseState)
    {
        if (ActiveTool == ScatteredArrangerTool.ApplyPalette && _applyPaletteHistory?.ModifiedElements.Count > 0)
        {
            AddHistoryAction(_applyPaletteHistory);
            _applyPaletteHistory = null;
        }
        else
            base.MouseUp(x, y, mouseState);
    }

    public override void MouseLeave()
    {
        if (ActiveTool == ScatteredArrangerTool.ApplyPalette && _applyPaletteHistory?.ModifiedElements.Count > 0)
        {
            AddHistoryAction(_applyPaletteHistory);
            _applyPaletteHistory = null;
        }
        else
            base.MouseLeave();
    }

    public override void MouseMove(double x, double y, MouseState mouseState)
    {
        int xc = Math.Clamp((int)x, 0, WorkingArranger.ArrangerPixelSize.Width - 1);
        int yc = Math.Clamp((int)y, 0, WorkingArranger.ArrangerPixelSize.Height - 1);

        if (ActiveTool == ScatteredArrangerTool.ApplyPalette && mouseState.LeftButtonPressed && _applyPaletteHistory is not null)
        {
            TryApplyPalette(xc, yc, SelectedPalette.Palette);
        }
        else if (ActiveTool == ScatteredArrangerTool.InspectElement)
        {
            var elX = xc / WorkingArranger.ElementPixelSize.Width;
            var elY = yc / WorkingArranger.ElementPixelSize.Height;
            var el = WorkingArranger.GetElement(elX, elY);

            if (el is ArrangerElement element)
            {
                var paletteName = element.Palette?.Name ?? "Default";
                var sourceName = element.Source switch
                {
                    FileDataSource fds => fds.FileLocation,
                    MemoryDataSource => "Memory",
                    _ => "None"
                };
                var fileOffsetDescription = $"0x{element.SourceAddress.ByteOffset:X}.{(element.SourceAddress.BitOffset != 0 ? element.SourceAddress.BitOffset.ToString() : "")}";

                ActivityMessage = WorkingArranger.ColorType switch
                {
                    PixelColorType.Indexed => $"Element ({elX}, {elY}): Codec {element.Codec.Name}, Palette {paletteName}, Source {sourceName}, FileOffset {fileOffsetDescription}",
                    PixelColorType.Direct => $"Element ({elX}, {elY}): Codec {element.Codec.Name}, Source {sourceName}, FileOffset {fileOffsetDescription}",
                    _ => "Unknown Color Type"
                };
            }
            else
            {
                ActivityMessage = $"Element ({elX}, {elY}): Empty";
            }
        }
        else if (ActiveTool == ScatteredArrangerTool.Select)
        {
            base.MouseMove(x, y, mouseState);
        }
    }
    #endregion

    //#region Drag and Drop Overrides
    //public override void DragOver(IDropInfo dropInfo)
    //{
    //    if (dropInfo.Data is not PaletteNodeViewModel nodeModel)
    //    {
    //        base.DragOver(dropInfo);
    //    }
    //    else if (WorkingArranger.ColorType == PixelColorType.Indexed)
    //    {
    //        var pal = nodeModel.Node.Item as Palette;
    //        dropInfo.DropTargetAdorner = DropTargetAdorners.Highlight;
    //        dropInfo.Effects = DragDropEffects.Move | DragDropEffects.Link;
    //    }
    //    else if (WorkingArranger.ColorType == PixelColorType.Direct)
    //    {

    //    }
    //    else
    //    {
    //        base.DragOver(dropInfo);
    //    }
    //}

    //public override void Drop(IDropInfo dropInfo)
    //{
    //    if (dropInfo.Data is PaletteNodeViewModel palNodeVM)
    //    {
    //        if (!_projectService.AreResourcesInSameProject(OriginatingProjectResource, palNodeVM.Node.Item))
    //        {
    //            var notifyEvent = new NotifyOperationEvent("Copying palettes across projects is not permitted");
    //            _events.PublishOnUIThread(notifyEvent);
    //            return;
    //        }

    //        var pal = palNodeVM.Node.Item as Palette;
    //        if (!Palettes.Any(x => ReferenceEquals(pal, x.Palette)))
    //        {
    //            var palModel = new PaletteModel(pal);
    //            Palettes.Add(palModel);
    //            SelectedPalette = palModel;
    //        }
    //    }
    //    else
    //        base.Drop(dropInfo);
    //}
    //#endregion

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
        if (WorkingArranger.Layout == ElementLayout.Single)
        {
            CreateGridlines(0, 0, WorkingArranger.ArrangerPixelSize.Width, WorkingArranger.ArrangerPixelSize.Height, 8, 8);
        }
        else if (WorkingArranger.Layout == ElementLayout.Tiled)
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
            //BitmapAdapter = new IndexedBitmapAdapter(_indexedImage);
            OnImageModified?.Invoke();
        }
        else if (WorkingArranger.ColorType == PixelColorType.Direct)
        {
            _directImage.Render();
            //BitmapAdapter = new DirectBitmapAdapter(_directImage);
            OnImageModified?.Invoke();
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
                    int elementX = posX * WorkingArranger.ElementPixelSize.Width;
                    int elementY = posY * WorkingArranger.ElementPixelSize.Height;
                    if (TryApplySinglePalette(elementX, elementY, SelectedPalette.Palette, false))
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
                        //_applyPaletteHistory.Add(pixelX, pixelY);
                        Render();
                        IsModified = true;
                        return true;
                    },
                    fail =>
                    {
                        //if (notify)
                        //    _events.PublishOnUIThread(new NotifyOperationEvent(fail.Reason));
                        return false;
                    });
            }
            else
                return false;
        }
    }

    public bool TryPickPalette(int pixelX, int pixelY)
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

    [RelayCommand]
    public async void ResizeArranger()
    {
        var model = new ResizeTiledScatteredArrangerViewModel(_windowManager, WorkingArranger.ArrangerElementSize.Width, WorkingArranger.ArrangerElementSize.Height);

        var dialogResult = await _windowManager.ShowDialog(model);

        if (dialogResult is not null)
        {
            WorkingArranger.Resize(dialogResult.Width, dialogResult.Height);
            CreateImages();
            AddHistoryAction(new ResizeArrangerHistoryAction(dialogResult.Width, dialogResult.Height));

            IsModified = true;
        }
    }

    [RelayCommand]
    public async void AssociatePalette()
    {
        var projectTree = _projectService.GetContainingProject(Resource);
        var palettes = projectTree.EnumerateDepthFirst()
            .Where(x => x.Item is Palette)
            .Select(x => new AssociatePaletteModel(x.Item as Palette, projectTree.CreatePathKey(x)))
            .Concat(_paletteService.GlobalPalettes.Select(x => new AssociatePaletteModel(x, x.Name)));

        var model = new AssociatePaletteViewModel(palettes);
        var dialogResult = await _windowManager.ShowDialog(model);

        if (dialogResult is not null)
        {
            var palModel = new PaletteModel(dialogResult.SelectedPalette.Palette, dialogResult.SelectedPalette.Palette.Entries);
            Palettes.Add(palModel);
        }
    }

    [RelayCommand]
    public void ToggleSnapMode()
    {
        if (SnapMode == SnapMode.Element)
            SnapMode = SnapMode.Pixel;
        else if (SnapMode == SnapMode.Pixel)
            SnapMode = SnapMode.Element;
    }

    [RelayCommand]
    public void ConfirmPendingOperation()
    {
        if (Paste?.Copy is ElementCopy)
            ApplyPaste(Paste);
    }

    /// <summary>
    /// Applies the paste as elements
    /// </summary>
    [RelayCommand]
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

        Messenger.Send(notifyEvent);
    }

    [RelayCommand]
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
        //if (action is PasteArrangerHistoryAction pasteAction)
        //{
        //    ApplyPasteInternal(pasteAction.Paste);
        //}
        //else if (action is DeleteElementSelectionHistoryAction deleteSelectionAction)
        //{
        //    DeleteElementSelection(deleteSelectionAction.Rect);
        //}
        //else if (action is ApplyPaletteHistoryAction applyPaletteAction)
        //{
        //    foreach (var location in applyPaletteAction.ModifiedElements)
        //    {
        //        _indexedImage.TrySetPalette(location.X, location.Y, applyPaletteAction.Palette);
        //    }
        //}
        //else if (action is ResizeArrangerHistoryAction resizeAction)
        //{
        //    WorkingArranger.Resize(resizeAction.Width, resizeAction.Height);
        //    CreateImages();
        //}
        //else if (action is RotateElementHistoryAction rotateAction)
        //{
        //    WorkingArranger.TryRotateElement(rotateAction.ElementX, rotateAction.ElementY, rotateAction.Rotation);
        //}
        //else if (action is MirrorElementHistoryAction mirrorAction)
        //{
        //    WorkingArranger.TryMirrorElement(mirrorAction.ElementX, mirrorAction.ElementY, mirrorAction.Mirror);
        //}
    }

    [RelayCommand]
    public override void Undo()
    {
        if (!CanUndo)
            return;

        var lastAction = UndoHistory[^1];
        UndoHistory.RemoveAt(UndoHistory.Count - 1);
        RedoHistory.Add(lastAction);
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));

        IsModified = UndoHistory.Count > 0;

        WorkingArranger = ((Arranger)Resource).CloneArranger();
        CreateImages();

        foreach (var action in UndoHistory)
            ApplyHistoryAction(action);

        Render();
    }

    [RelayCommand]
    public override void Redo()
    {
        if (!CanRedo)
            return;

        var redoAction = RedoHistory[^1];
        RedoHistory.RemoveAt(RedoHistory.Count - 1);
        UndoHistory.Add(redoAction);
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));

        ApplyHistoryAction(redoAction);
        IsModified = true;
        Render();
    }
    #endregion
}
