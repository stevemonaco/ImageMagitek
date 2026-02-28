using TileShop.Shared.Models;
using TileShop.Shared.Tools;
using TileShop.UI.ViewModels;

namespace TileShop.UI.Features.Graphics.Tools;

public class ApplyPaletteToolHandler : IToolHandler<GraphicsEditorViewModel>
{
    private ApplyPaletteHistoryAction? _activeHistory;

    public ToolResult OnMouseDown(ToolContext ctx, GraphicsEditorViewModel state)
    {
        if (!ctx.MouseState.LeftButtonPressed || state.SelectedPalette is null || !state.IsIndexedColor)
            return ToolResult.Unhandled;

        _activeHistory = new ApplyPaletteHistoryAction(state.SelectedPalette.Palette);
        state.TryApplyPalette(ctx.PixelX, ctx.PixelY, state.SelectedPalette.Palette);
        return ToolResult.HandledDisplay;
    }

    public ToolResult OnMouseMove(ToolContext ctx, GraphicsEditorViewModel state)
    {
        if (ctx.MouseState.LeftButtonPressed && _activeHistory is not null &&
            state.SelectedPalette is not null && state.IsIndexedColor)
        {
            state.TryApplyPalette(ctx.PixelX, ctx.PixelY, state.SelectedPalette.Palette);
            return ToolResult.HandledDisplay;
        }

        state.UpdateActivityMessage(ctx.PixelX, ctx.PixelY);
        return ToolResult.Unhandled;
    }

    public ToolResult OnMouseUp(ToolContext ctx, GraphicsEditorViewModel state)
    {
        return new ToolResult(FinalizeHistory(state), InvalidationLevel.None);
    }

    public ToolResult OnKeyDown(ToolContext ctx, GraphicsEditorViewModel state) => ToolResult.Unhandled;
    public ToolResult OnKeyUp(ToolContext ctx, GraphicsEditorViewModel state) => ToolResult.Unhandled;

    public HistoryAction? Deactivate(GraphicsEditorViewModel state)
    {
        var action = TakeHistory();
        return action?.ModifiedElements.Count > 0 ? action : null;
    }

    private bool FinalizeHistory(GraphicsEditorViewModel state)
    {
        if (_activeHistory?.ModifiedElements.Count > 0)
        {
            state.AddHistoryAction(_activeHistory);
            _activeHistory = null;
            return true;
        }

        _activeHistory = null;
        return false;
    }

    private ApplyPaletteHistoryAction? TakeHistory()
    {
        var history = _activeHistory;
        _activeHistory = null;
        return history;
    }
}
