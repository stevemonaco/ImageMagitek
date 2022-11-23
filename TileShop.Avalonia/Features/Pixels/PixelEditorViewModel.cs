using System;
using ImageMagitek;
using ImageMagitek.Services;
using TileShop.Shared.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TileShop.Shared.Input;
using TileShop.Shared.Interactions;

namespace TileShop.AvaloniaUI.ViewModels;

public enum PixelTool { Select, Pencil, ColorPicker, FloodFill }
public enum ColorPriority { Primary, Secondary }

public abstract partial class PixelEditorViewModel<TColor> : ArrangerEditorViewModel
    where TColor : struct
{
    protected readonly Arranger _projectArranger;
    protected int _viewWidth;
    protected int _viewHeight;
    protected PixelTool? _priorTool;
    protected PencilHistoryAction<TColor>? _activePencilHistory;

    [ObservableProperty] private bool _isDrawing;
    [ObservableProperty] private PixelTool _activeTool = PixelTool.Pencil;

    [ObservableProperty] private TColor _activeColor;
    [ObservableProperty] private TColor _primaryColor;
    [ObservableProperty] private TColor _secondaryColor;

    public PixelEditorViewModel(Arranger projectArranger, IInteractionService interactionService, IPaletteService paletteService) :
        base(projectArranger, interactionService, paletteService)
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

    [RelayCommand]
    public void ChangeTool(PixelTool tool)
    {
        ActiveTool = tool;
    }

    public void PushTool(PixelTool tool)
    {
        _priorTool = ActiveTool;
        ActiveTool = tool;
    }

    public void PopTool()
    {
        ActiveTool = _priorTool ?? ActiveTool;
        _priorTool = null;
    }

    [RelayCommand] public void SetPrimaryColor(TColor color) => PrimaryColor = color;
    [RelayCommand] public void SetSecondaryColor(TColor color) => SecondaryColor = color;

    [RelayCommand]
    public virtual void ConfirmPendingOperation()
    {
        if (Paste?.Copy is ElementCopy || Paste?.Copy is IndexedPixelCopy || Paste?.Copy is DirectPixelCopy)
            ApplyPaste(Paste);
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

        ReloadImage();

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

    #region Input Actions
    public override void KeyPress(KeyState keyState, double? x, double? y)
    {
        //if (keyState.Modifiers.HasFlag(KeyModifiers.Alt) && x.HasValue && y.HasValue && Paste is null)
        if (keyState.Key == SecondaryAltKey && x.HasValue && y.HasValue && Paste is null && _priorTool is null)
        {
            PushTool(PixelTool.ColorPicker);

        }
        base.KeyPress(keyState, x, y);
    }

    public override void KeyUp(KeyState keyState, double? x, double? y)
    {
        //if (keyState.Modifiers.HasFlag(KeyModifiers.Alt) && x.HasValue && y.HasValue && Paste is null)
        if (keyState.Key == SecondaryAltKey && x.HasValue && y.HasValue && Paste is null)
        {
            PopTool();

        }
        base.KeyPress(keyState, x, y);
    }

    public override void MouseDown(double x, double y, MouseState mouseState)
    {
        var bounds = WorkingArranger.ArrangerPixelSize;
        int xc = Math.Clamp((int)x, 0, bounds.Width - 1);
        int yc = Math.Clamp((int)y, 0, bounds.Height - 1);

        if ((ActiveTool == PixelTool.ColorPicker || mouseState.Modifiers.HasFlag(KeyModifiers.Alt)) && mouseState.LeftButtonPressed)
        {
            PickColor(xc, yc, ColorPriority.Primary);
        }
        else if ((ActiveTool == PixelTool.ColorPicker || mouseState.Modifiers.HasFlag(KeyModifiers.Alt)) && mouseState.RightButtonPressed)
        {
            PickColor(xc, yc, ColorPriority.Secondary);
        }
        else if (ActiveTool == PixelTool.Pencil && mouseState.LeftButtonPressed)
        {
            StartDraw(xc, yc, ColorPriority.Primary);
            SetPixel(xc, yc, PrimaryColor);
        }
        else if (ActiveTool == PixelTool.Pencil && mouseState.RightButtonPressed)
        {
            StartDraw(xc, yc, ColorPriority.Secondary);
            SetPixel(xc, yc, SecondaryColor);
        }
        else if (ActiveTool == PixelTool.FloodFill && mouseState.LeftButtonPressed)
        {
            FloodFill(xc, yc, PrimaryColor);
        }
        else if (ActiveTool == PixelTool.FloodFill && mouseState.RightButtonPressed)
        {
            FloodFill(xc, yc, SecondaryColor);
        }
        else
        {
            base.MouseDown(x, y, mouseState);
        }
    }

    public override void MouseUp(double x, double y, MouseState mouseState)
    {
        if (IsDrawing && !mouseState.LeftButtonPressed && !mouseState.RightButtonPressed)
        {
            StopDrawing();
        }
        else
        {
            base.MouseUp(x, y, mouseState);
        }
    }

    public override void MouseMove(double x, double y, MouseState mouseState)
    {
        var bounds = WorkingArranger.ArrangerPixelSize;
        int xc = Math.Clamp((int)x, 0, bounds.Width - 1);
        int yc = Math.Clamp((int)y, 0, bounds.Height - 1);
        LastMousePosition = new(xc, yc);

        if (x < 0 || x >= bounds.Width || y < 0 || y >= bounds.Height)
        {
            LastMousePosition = null;
            return;
        }

        if (IsDrawing && ActiveTool == PixelTool.Pencil && mouseState.LeftButtonPressed)
            SetPixel(xc, yc, PrimaryColor);
        else if (IsDrawing && ActiveTool == PixelTool.Pencil && mouseState.RightButtonPressed)
            SetPixel(xc, yc, SecondaryColor);
        else
            base.MouseMove(x, y, mouseState);
    }

    public override void MouseLeave()
    {
        if (ActiveTool == PixelTool.Pencil && IsDrawing)
        {
            StopDrawing();
        }
        else
        {
            PopTool();
            base.MouseLeave();
        }
    }
    #endregion
}
