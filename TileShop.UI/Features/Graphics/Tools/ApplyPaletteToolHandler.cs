using TileShop.Shared.Input;
using TileShop.Shared.Models;
using TileShop.UI.ViewModels;

namespace TileShop.UI.Features.Graphics.Tools;

public class ApplyPaletteToolHandler : IToolHandler<GraphicsEditorViewModel>
{
    private ApplyPaletteHistoryAction? _activeHistory;

    public bool OnMouseDown(ToolContext ctx, GraphicsEditorViewModel state)
    {
        if (!ctx.MouseState.LeftButtonPressed || state.SelectedPalette is null || !state.IsIndexedColor)
            return false;

        _activeHistory = new ApplyPaletteHistoryAction(state.SelectedPalette.Palette);
        state.TryApplyPalette(ctx.PixelX, ctx.PixelY, state.SelectedPalette.Palette);
        return true;
    }

    public bool OnMouseMove(ToolContext ctx, GraphicsEditorViewModel state)
    {
        if (ctx.MouseState.LeftButtonPressed && _activeHistory is not null &&
            state.SelectedPalette is not null && state.IsIndexedColor)
        {
            state.TryApplyPalette(ctx.PixelX, ctx.PixelY, state.SelectedPalette.Palette);
            return true;
        }

        state.UpdateActivityMessage(ctx.PixelX, ctx.PixelY);
        return false;
    }

    public bool OnMouseUp(ToolContext ctx, GraphicsEditorViewModel state)
    {
        return FinalizeHistory(state);
    }

    public bool OnKeyDown(ToolContext ctx, GraphicsEditorViewModel state) => false;
    public bool OnKeyUp(ToolContext ctx, GraphicsEditorViewModel state) => false;

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
