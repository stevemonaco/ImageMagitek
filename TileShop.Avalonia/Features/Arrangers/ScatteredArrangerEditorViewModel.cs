using System;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using ImageMagitek;
using ImageMagitek.Codec;
using ImageMagitek.Colors;
using ImageMagitek.ExtensionMethods;
using ImageMagitek.Services;
using ImageMagitek.Services.Stores;
using Jot;
using Monaco.PathTree;
using TileShop.AvaloniaUI.Imaging;
using TileShop.AvaloniaUI.Models;
using TileShop.Shared.Input;
using TileShop.Shared.Interactions;
using TileShop.Shared.Messages;
using TileShop.Shared.Models;
using Point = System.Drawing.Point;

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
    private IndexedImage? _indexedImage;
    private DirectImage? _directImage;

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

    public ScatteredArrangerEditorViewModel(Arranger arranger, IInteractionService interactionService,
        IColorFactory colorFactory, PaletteStore paletteStore, IProjectService projectService, Tracker tracker, AppSettings settings) :
        base(arranger, interactionService, colorFactory, paletteStore, tracker)
    {
        AreSymmetryToolsEnabled = settings.EnableArrangerSymmetryTools;

        CreateImages();
        _gridSettings = GridSettingsViewModel.CreateDefault(_indexedImage!);
        //_gridSettings = GridSettingsViewModel.CreateDefault(arranger);

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
        palettes.ExceptWith(_paletteStore.GlobalPalettes);

        var palModels = palettes.OrderBy(x => x.Name)
            .Concat(_paletteStore.GlobalPalettes.OrderBy(x => x.Name))
            .Select(x => new PaletteModel(x));

        Selection = new(WorkingArranger, SnapMode);
        Palettes = new(palModels);
        _selectedPalette = Palettes.First();
        _projectService = projectService;
    }

    public void SetSelectToolMode() => ActiveTool = ScatteredArrangerTool.Select;

    public void SetApplyPaletteMode() => ActiveTool = ScatteredArrangerTool.ApplyPalette;

    [RelayCommand]
    public override async Task SaveChangesAsync()
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

        await _projectService.SaveResource(projectTree, resourceNode!, true).Match(
                 success =>
                 {
                     UndoHistory.Clear();
                     RedoHistory.Clear();
                     OnPropertyChanged(nameof(CanUndo));
                     OnPropertyChanged(nameof(CanRedo));

                     IsModified = false;
                     return Task.CompletedTask;
                 },
                 async fail => await _interactions.AlertAsync("Project Error", $"An error occurred while saving the project tree to {projectTree.Root.DiskLocation}: {fail.Reason}")
             );
    }

    public override void DiscardChanges()
    {
        WorkingArranger = ((Arranger)Resource).CloneArranger();
        CreateImages();
        GridSettings.AdjustGridlines(WorkingArranger);
        IsModified = false;
    }

    #region Mouse Actions
    public override void MouseDown(double x, double y, MouseState mouseState)
    {
        int xc = Math.Clamp((int)x, 0, WorkingArranger.ArrangerPixelSize.Width - 1);
        int yc = Math.Clamp((int)y, 0, WorkingArranger.ArrangerPixelSize.Height - 1);
        var elementX = xc / WorkingArranger.ElementPixelSize.Width;
        var elementY = yc / WorkingArranger.ElementPixelSize.Height;

        if (ActiveTool == ScatteredArrangerTool.ApplyPalette && mouseState.LeftButtonPressed && SelectedPalette is not null)
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
        LastMousePosition = null;

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

        LastMousePosition = new(xc, yc);

        if (mouseState.Modifiers.HasFlag(KeyModifiers.Shift) && Paste is null)
        {
            if (TryStartNewSingleSelection(x, y))
            {
                CompleteSelection();
                //var copy = new ElementCopy(WorkingArranger, (int)(Selection.SelectionRect.Left / WorkingArranger.ElementPixelSize.Width), (int)(Selection.SelectionRect.Right / WorkingArranger.ElementPixelSize.Width), 8, 8);
                //Paste = new ArrangerPaste(copy, SnapMode.Element);
                return;
            }
        }

        if (ActiveTool == ScatteredArrangerTool.ApplyPalette && mouseState.LeftButtonPressed && _applyPaletteHistory is not null && SelectedPalette is not null)
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
                string paletteName = "Default";
                if (element.Codec is IIndexedCodec codec)
                    paletteName = codec.Palette.Name;

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

    public override void KeyPress(KeyState keyState, double? x, double? y)
    {
        if (keyState.Key == SecondaryAltKey && x.HasValue && y.HasValue && Paste is null)
        {
            if (TryStartNewSingleSelection(x!.Value, y!.Value))
            {
                CompleteSelection();
                return;
            }
        }
        else
        {
            base.KeyPress(keyState, x, y);
        }
    }

    public override void KeyUp(KeyState keyState, double? x, double? y)
    {
        if (keyState.Key == SecondaryAltKey && x.HasValue && y.HasValue && Paste is null &&
            WorkingArranger.ElementPixelSize == new Size(Selection.SelectionRect.SnappedWidth, Selection.SelectionRect.SnappedHeight))
        {
            CancelOverlay();
        }
        else
        {
            base.KeyPress(keyState, x, y);
        }
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
    }

    public override void Render()
    {
        CancelOverlay();

        if (WorkingArranger.ColorType == PixelColorType.Indexed)
        {
            _indexedImage?.Render();
            BitmapAdapter.Invalidate();
            OnImageModified?.Invoke();
        }
        else if (WorkingArranger.ColorType == PixelColorType.Direct)
        {
            _directImage?.Render();
            BitmapAdapter.Invalidate();
            OnImageModified?.Invoke();
        }
    }

    private MagitekResult ApplyPasteInternal(ArrangerPaste paste)
    {
        if (WorkingArranger is not ScatteredArranger arranger)
            return new MagitekResult.Failed($"Pasting elements into a '{WorkingArranger.GetType()}' is not supported");

        if (paste?.Copy is not ElementCopy elementCopy)
            return new MagitekResult.Failed("No valid Paste selection");

        if (!_projectService.AreResourcesInSameProject(elementCopy.ProjectResource, OriginatingProjectResource))
            return new MagitekResult.Failed("Copying arranger elements across projects is not permitted");

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

        return ElementCopier.CopyElements(elementCopy, arranger, sourceStart, destStart, copyWidth, copyHeight);
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
            Guard.IsNotNull(_indexedImage);

            if (pixelX >= WorkingArranger.ArrangerPixelSize.Width || pixelY >= WorkingArranger.ArrangerPixelSize.Height)
                return false;

            var el = WorkingArranger.GetElementAtPixel(pixelX, pixelY);

            if (el is ArrangerElement { Codec: IIndexedCodec codec } element)
            {
                if (ReferenceEquals(palette, codec.Palette))
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
                        //    _events.PublishOnUIThread(new NotifyStatusEvent(fail.Reason));
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

        if (el is ArrangerElement { Codec: IIndexedCodec codec } element)
        {
            SelectedPalette = Palettes.FirstOrDefault(x => ReferenceEquals(codec.Palette, x.Palette)) ??
                Palettes.First(x => ReferenceEquals(_paletteStore.DefaultPalette, x.Palette));
        }

        return true;
    }

    [RelayCommand]
    public void ChangeTool(ScatteredArrangerTool tool)
    {
        ActiveTool = tool;
    }

    [RelayCommand]
    public void ToggleSymmetryTools()
    {
        AreSymmetryToolsEnabled = !AreSymmetryToolsEnabled;
    }

    [RelayCommand]
    public async Task ResizeArranger()
    {
        var model = new ResizeTiledScatteredArrangerViewModel(_interactions, WorkingArranger.ArrangerElementSize.Width, WorkingArranger.ArrangerElementSize.Height);

        var dialogResult = await _interactions.RequestAsync(model);

        if (dialogResult is not null)
        {
            WorkingArranger.Resize(dialogResult.Width, dialogResult.Height);
            CreateImages();
            GridSettings = GridSettingsViewModel.CreateDefault(_indexedImage!);
            AddHistoryAction(new ResizeArrangerHistoryAction(dialogResult.Width, dialogResult.Height));

            IsModified = true;
        }
    }

    [RelayCommand]
    public async Task AssociatePalette()
    {
        var projectTree = _projectService.GetContainingProject(Resource);
        var palettes = projectTree.EnumerateDepthFirst()
            .Where(x => x.Item is Palette)
            .Select(x => new AssociatePaletteModel((Palette)x.Item, projectTree.CreatePathKey(x)))
            .Concat(_paletteStore.GlobalPalettes.Select(x => new AssociatePaletteModel(x, x.Name)));

        var model = new AssociatePaletteViewModel(palettes);
        var dialogResult = await _interactions.RequestAsync(model);

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
        var message = ApplyPasteInternal(paste).Match(
            success =>
            {
                AddHistoryAction(new PasteArrangerHistoryAction(paste));
                IsModified = true;
                Render();
                return new NotifyStatusMessage("Paste successfully applied");
            },
            fail => new NotifyStatusMessage(fail.Reason)
            );

        Messenger.Send(message);
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

        if (_indexedImage is not null)
            GridSettings = GridSettingsViewModel.CreateDefault(_indexedImage);
        else if (_directImage is not null)
            GridSettings = GridSettingsViewModel.CreateDefault(_directImage);

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
