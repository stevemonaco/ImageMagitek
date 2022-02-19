using ImageMagitek;
using ImageMagitek.Services;
using TileShop.Shared.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using TileShop.AvaloniaUI.ViewExtenders;
using CommunityToolkit.Mvvm.Input;

namespace TileShop.AvaloniaUI.ViewModels;

public enum PixelTool { Select, Pencil, ColorPicker, FloodFill }
public enum ColorPriority { Primary, Secondary }

public abstract partial class PixelEditorViewModel<TColor> : ArrangerEditorViewModel
    where TColor : struct
{
    protected readonly Arranger _projectArranger;
    protected int _viewX;
    protected int _viewY;
    protected int _viewWidth;
    protected int _viewHeight;
    protected PencilHistoryAction<TColor>? _activePencilHistory;

    [ObservableProperty] private bool _isDrawing;
    [ObservableProperty] private PixelTool _activeTool = PixelTool.Pencil;
    [ObservableProperty] private TColor _activeColor;
    [ObservableProperty] private TColor _primaryColor;
    [ObservableProperty] private TColor _secondaryColor;

    public PixelEditorViewModel(Arranger projectArranger, IWindowManager windowManager, IPaletteService paletteService) :
        base(windowManager, paletteService)
    {
        DisplayName = "Pixel Editor";
        CanAcceptElementPastes = true;
        CanAcceptPixelPastes = true;
        SnapMode = SnapMode.Pixel;

        OriginatingProjectResource = projectArranger;
        _projectArranger = projectArranger;
    }

    protected abstract void ReloadImage();
    public abstract void SetPixel(int x, int y, TColor color);
    public abstract TColor GetPixel(int x, int y);
    public abstract void FloodFill(int x, int y, TColor fillColor);

    [ICommand] public void SetPrimaryColor(TColor color) => PrimaryColor = color;
    [ICommand] public void SetSecondaryColor(TColor color) => SecondaryColor = color;

    [ICommand]
    public virtual void ConfirmPendingOperation()
    {
        if (Paste?.Copy is ElementCopy || Paste?.Copy is IndexedPixelCopy || Paste?.Copy is DirectPixelCopy)
            ApplyPaste(Paste);
    }

    [ICommand]
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

        ReloadImage();

        foreach (var action in UndoHistory)
            ApplyHistoryAction(action);

        Render();
    }

    [ICommand]
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

    #region Commands
    public virtual void StartDraw(int x, int y, ColorPriority priority)
    {
        if (priority == ColorPriority.Primary)
            _activePencilHistory = new PencilHistoryAction<TColor>(PrimaryColor);
        else if (priority == ColorPriority.Secondary)
            _activePencilHistory = new PencilHistoryAction<TColor>(SecondaryColor);
        IsDrawing = true;
    }

    public virtual void StopDrawing()
    {
        if (IsDrawing && _activePencilHistory?.ModifiedPoints.Count > 0)
        {
            IsDrawing = false;
            AddHistoryAction(_activePencilHistory);
            _activePencilHistory = null;
        }
    }

    /// <summary>
    /// Pick a color at the specified coordinate
    /// </summary>
    /// <param name="x">x-coordinate in pixel coordinates</param>
    /// <param name="y">y-coordinate in pixel coordinates</param>
    /// <param name="priority">Priority to apply the color pick to</param>
    public virtual void PickColor(int x, int y, ColorPriority priority)
    {
        var color = GetPixel(x, y);

        if (priority == ColorPriority.Primary)
            PrimaryColor = color;
        else if (priority == ColorPriority.Secondary)
            SecondaryColor = color;
    }
    #endregion

    #region Mouse Actions
    //public override void StartDrag(IDragInfo dragInfo)
    //{
    //    if (Selection.HasSelection)
    //    {
    //        var rect = Selection.SelectionRect;

    //        ArrangerCopy copy = default;

    //        if (SnapMode == SnapMode.Pixel && WorkingArranger.ColorType == PixelColorType.Indexed)
    //        {
    //            copy = WorkingArranger.CopyPixelsIndexed(rect.SnappedLeft + _viewX, rect.SnappedTop + _viewY, rect.SnappedWidth, rect.SnappedHeight);
    //        }
    //        else if (SnapMode == SnapMode.Pixel && WorkingArranger.ColorType == PixelColorType.Direct)
    //        {
    //            copy = WorkingArranger.CopyPixelsDirect(rect.SnappedLeft + _viewX, rect.SnappedTop + _viewY, rect.SnappedWidth, rect.SnappedHeight);
    //        }

    //        var paste = new ArrangerPaste(copy, SnapMode)
    //        {
    //            DeltaX = (int)dragInfo.DragStartPosition.X - Selection.SelectionRect.SnappedLeft,
    //            DeltaY = (int)dragInfo.DragStartPosition.Y - Selection.SelectionRect.SnappedTop
    //        };
    //        dragInfo.Data = paste;
    //        dragInfo.Effects = DragDropEffects.Copy | DragDropEffects.Move;

    //        Selection = new ArrangerSelection(WorkingArranger, SnapMode);
    //    }
    //    else
    //    {
    //        base.StartDrag(dragInfo);
    //    }
    //}
    #endregion
}
