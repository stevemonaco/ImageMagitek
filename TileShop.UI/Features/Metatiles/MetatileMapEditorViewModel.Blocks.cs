using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using ImageMagitek.Metatiles;
using ImageMagitek.Tilemaps;
using TileShop.Shared.Input;
using TileShop.Shared.Messages;
using TileShop.Shared.Models;
using TileShop.Shared.Tools;
using TileShop.UI.Features.Graphics.Tools;

namespace TileShop.UI.ViewModels;

public partial class MetatileMapEditorViewModel
{
    private readonly ToolInputRouter<MetatileMapEditorViewModel> _toolRouter;
    private BlockGrid _grid;
    private BlockGrid _baseGrid;
    private MetatileMapPlotter _plotter;
    private readonly CellStroke _stroke = new();

    private readonly Dictionary<TileTool, IToolHandler<MetatileMapEditorViewModel>> _blockTools = new()
    {
        [TileTool.Pencil] = new TileStrokeToolHandler<MetatileMapEditorViewModel>("Plot Blocks", (s, cell) => s.PlotStroke(cell),
            (s, ctx) => s.BrushFootprint(s.CellAt(ctx.PixelX, ctx.PixelY))),
        [TileTool.RectangleFill] = new CellRectangleToolHandler<MetatileMapEditorViewModel>((s, cells) => s.FillBlocks(cells), (s, cell) => s.BrushFootprint(cell)),
        [TileTool.Picker] = new CellRectangleToolHandler<MetatileMapEditorViewModel>((s, cells) => s.PickBlocks(cells)),
        [TileTool.Erase] = new TileStrokeToolHandler<MetatileMapEditorViewModel>("Erase Blocks", (s, cell) => s.EraseStroke(cell),
            (s, ctx) => s.CellsToPixels(new Rectangle(s.CellAt(ctx.PixelX, ctx.PixelY), new Size(1, 1)))),
    };

    [ObservableProperty] private TileTool _selectedBlockTool = TileTool.Pencil;

    public TileTool DisplayedBlockTool => _toolRouter.IsOverrideEngaged ? TileTool.Picker : SelectedBlockTool;

    /// <summary>
    /// The block grid the editor plots into and Save writes
    /// </summary>
    public BlockGrid Grid => _grid;

    public Point? LastMousePosition { get; private set; }

    /// <summary>Pixel region the active tool would act on at the pointer position, or null if none.</summary>
    public Rectangle? HoverTarget { get; private set; }

    public IReadOnlyList<Key> AlternativeToolKeys { get; } = [Key.LeftControl, Key.RightControl];

    public bool IsCellStrokeActive => _stroke.Name is not null;

    private IToolHandler<MetatileMapEditorViewModel>? SelectedTool() => IsBlocksMode ? _blockTools[SelectedBlockTool] : null;

    private IToolHandler<MetatileMapEditorViewModel>? TemporaryTool() => IsBlocksMode ? _blockTools[TileTool.Picker] : null;

    partial void OnSelectedBlockToolChanged(TileTool oldValue, TileTool newValue)
    {
        OnPropertyChanged(nameof(DisplayedBlockTool));

        if (_blockTools[oldValue].Deactivate(this) is { } historyAction)
            AddHistoryAction(historyAction);
    }

    [RelayCommand]
    private void ChangeBlockTool(TileTool tool) => SelectedBlockTool = tool;

    private void NotifyDisplayedToolChanged() => OnPropertyChanged(nameof(DisplayedBlockTool));

    public Point CellAt(int pixelX, int pixelY) => new(pixelX / Map.BlockPixelSize.Width, pixelY / Map.BlockPixelSize.Height);

    public Rectangle? CellsToPixels(Rectangle cells) => CellRect.ToPixels(cells, Map.BlockPixelSize, new Size(Map.Width, Map.Height));

    /// <summary>
    /// Where the brush would land for a pointer over <paramref name="cell"/>, snapped to the stroke's grid during a stroke
    /// </summary>
    private Rectangle? BrushFootprint(Point cell)
    {
        if (BlockPalette?.Brush is not { } brush)
            return null;

        if (_stroke.Origin is { } origin)
            cell = BrushStroke.SnapToStroke(brush.Size, origin, cell);

        return CellsToPixels(new Rectangle(cell, brush.Size));
    }

    public void BeginCellStroke(string name)
    {
        EndCellStroke();
        _stroke.Begin(name);
    }

    public void EndCellStroke()
    {
        if (_stroke.End() is { } name)
            AddHistoryAction(new PlotBlocksHistoryAction(name, _grid.Clone()));
    }

    /// <summary>
    /// Stamps the brush at every block from the previous stroke block to <paramref name="cell"/>
    /// </summary>
    public void PlotStroke(Point cell)
    {
        if (!TryGetBrush(out var brush))
            return;

        var origin = _stroke.Origin ??= cell;
        var changed = false;
        foreach (var point in _stroke.Advance(cell))
        {
            var snapped = BrushStroke.SnapToStroke(brush.Size, origin, point);
            changed |= _plotter.Stamp(brush, snapped.X, snapped.Y);
        }

        CompleteBlockEdit(changed);
    }

    public void EraseStroke(Point cell)
    {
        var changed = false;
        foreach (var point in _stroke.Advance(cell))
            changed |= _plotter.Erase(new Rectangle(point.X, point.Y, 1, 1));

        CompleteBlockEdit(changed);
    }

    public void FillBlocks(Rectangle cells)
    {
        if (!TryGetBrush(out var brush))
            return;

        BeginCellStroke("Fill Blocks");
        CompleteBlockEdit(_plotter.Fill(brush, cells));
        EndCellStroke();
    }

    /// <summary>
    /// Makes the blocks under <paramref name="cells"/> the brush
    /// </summary>
    public void PickBlocks(Rectangle cells)
    {
        if (_plotter.Pick(cells) is { } brush)
            BlockPalette?.SetBrush(brush);
        else
            Messenger.Send(new NotifyStatusMessage("No block of the metatile sets to pick there"));
    }

    private bool TryGetBrush(out BlockBrush brush)
    {
        brush = BlockPalette?.Brush!;
        var error = brush is null ? "Pick a block from the block palette" : _plotter.FindBrushError(brush);
        if (error is null)
            return true;

        if (_stroke.ShouldReport())
            Messenger.Send(new NotifyStatusMessage(error));

        return false;
    }

    private void CompleteBlockEdit(bool changed)
    {
        if (!changed)
            return;

        _stroke.Changed = true;
        IsModified = true;
        RedrawMap();
    }

    public override void AddHistoryAction(HistoryAction action)
    {
        base.AddHistoryAction(action);
        NotifyHistoryChanged();
    }

    private void ClearHistory()
    {
        UndoHistory.Clear();
        RedoHistory.Clear();
        NotifyHistoryChanged();
    }

    private void NotifyHistoryChanged()
    {
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanUndo))]
    public override void Undo()
    {
        if (!CanUndo)
            return;

        var action = UndoHistory[^1];
        UndoHistory.RemoveAt(UndoHistory.Count - 1);
        RedoHistory.Add(action);

        var restored = UndoHistory.OfType<PlotBlocksHistoryAction>().LastOrDefault()?.After ?? _baseGrid;
        SetGrid(restored.Clone());
        NotifyHistoryChanged();
        IsModified = CanUndo;
        RedrawMap();
    }

    [RelayCommand(CanExecute = nameof(CanRedo))]
    public override void Redo()
    {
        if (!CanRedo)
            return;

        var action = RedoHistory[^1];
        RedoHistory.RemoveAt(RedoHistory.Count - 1);
        UndoHistory.Add(action);

        if (action is PlotBlocksHistoryAction plot)
            SetGrid(plot.After.Clone());

        NotifyHistoryChanged();
        IsModified = true;
        RedrawMap();
    }

    public void UpdateActivityMessage(int pixelX, int pixelY)
    {
        var cell = CellAt(pixelX, pixelY);
        var (bx, by) = (cell.X, cell.Y);
        if (bx < 0 || by < 0 || bx >= Map.Width || by >= Map.Height)
        {
            ActivityMessage = string.Empty;
            return;
        }

        if (_grid[bx, by] is not { } number)
        {
            ActivityMessage = $"({bx}, {by})  No block data";
            return;
        }

        var parts = new List<string> { $"({bx}, {by})  Block {number}" };
        var set = Map.FindSetFor(number);
        if (set is not null)
            parts.Add(set.Set.Name);

        if (Map.BlockBitsValue(bx, by) is { } bits)
            parts.Add($"Bits {bits}");

        if (set?.Set.ReadAttribute(number - set.Base) is { } attribute)
            parts.Add($"Attribute 0x{attribute.ToString($"X{set.Set.AttributeSize * 2}")}");

        ActivityMessage = string.Join("  ·  ", parts);
    }

    public ToolCursor ResolveCursor(KeyModifiers modifiers)
    {
        var cursor = _toolRouter.Resolve(modifiers)?.Cursor ?? ToolCursor.Default;
        return cursor == ToolCursor.Crosshair && HoverTarget is null ? ToolCursor.NotAllowed : cursor;
    }

    public bool ContainsPoint(double x, double y) => x >= 0 && y >= 0 && x < Map.PixelSize.Width && y < Map.PixelSize.Height;

    private void SetHoverTarget(Rectangle? target)
    {
        if (target == HoverTarget)
            return;

        HoverTarget = target;
        OnImageModified?.Invoke();
    }

    private void RefreshHoverTarget(KeyModifiers modifiers)
    {
        if (LastMousePosition is not { } pos)
        {
            SetHoverTarget(null);
            return;
        }

        var ctx = new ToolContext(pos.X, pos.Y, pos.X, pos.Y, new MouseState(false, false, false, modifiers));
        SetHoverTarget(_toolRouter.Resolve(modifiers)?.GetTargetRect(ctx, this));
    }

    private ToolContext CreateContext(double x, double y, MouseState mouseState) =>
        new(x, y, Math.Clamp((int)x, 0, Map.PixelSize.Width - 1), Math.Clamp((int)y, 0, Map.PixelSize.Height - 1), mouseState);

    /// <param name="x">Pointer position in image pixels</param>
    public bool MouseDown(double x, double y, MouseState mouseState)
    {
        var result = _toolRouter.Press(CreateContext(x, y, mouseState), this);
        if (result.Invalidation != InvalidationLevel.None)
            OnImageModified?.Invoke();
        return result.Handled;
    }

    public bool MouseUp(double x, double y, MouseState mouseState)
    {
        var wasOverrideEngaged = _toolRouter.IsOverrideEngaged;
        var result = _toolRouter.Release(CreateContext(x, y, mouseState), this);

        if (_toolRouter.IsOverrideEngaged != wasOverrideEngaged)
            NotifyDisplayedToolChanged();

        if (result.Invalidation != InvalidationLevel.None)
            OnImageModified?.Invoke();
        return result.Handled;
    }

    public bool MouseMove(double x, double y, MouseState mouseState)
    {
        if (!ContainsPoint(x, y))
        {
            _stroke.Break();

            LastMousePosition = null;
            ActivityMessage = string.Empty;
            SetHoverTarget(null);
            return false;
        }

        var ctx = CreateContext(x, y, mouseState);
        LastMousePosition = new Point(ctx.PixelX, ctx.PixelY);

        var wasOverrideEngaged = _toolRouter.IsOverrideEngaged;
        var result = _toolRouter.Move(ctx, this);

        if (_toolRouter.IsOverrideEngaged != wasOverrideEngaged)
            NotifyDisplayedToolChanged();

        if (!IsBlocksMode)
            UpdateActivityMessage(ctx.PixelX, ctx.PixelY);

        SetHoverTarget(_toolRouter.Resolve(mouseState.Modifiers)?.GetTargetRect(ctx, this));
        return result.Handled;
    }

    public void MouseLeave()
    {
        LastMousePosition = null;
        ActivityMessage = string.Empty;
        SetHoverTarget(null);

        if (_toolRouter.EndGesture(this) is { } historyAction)
            AddHistoryAction(historyAction);
    }

    public void PointerCaptureLost()
    {
        if (_toolRouter.IsGestureActive && _toolRouter.EndGesture(this) is { } historyAction)
            AddHistoryAction(historyAction);
    }

    public bool KeyPress(KeyState keyState)
    {
        var handled = _toolRouter.KeyDown(keyState.Key);
        if (handled)
            NotifyDisplayedToolChanged();

        RefreshHoverTarget(keyState.Modifiers);
        return handled;
    }

    public void KeyUp(KeyState keyState)
    {
        if (_toolRouter.KeyUp(keyState.Key))
            NotifyDisplayedToolChanged();

        RefreshHoverTarget(keyState.Modifiers);
    }
}
