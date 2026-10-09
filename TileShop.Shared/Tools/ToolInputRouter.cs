using System;
using System.Collections.Generic;
using System.Linq;
using TileShop.Shared.Input;
using TileShop.Shared.Models;

namespace TileShop.Shared.Tools;

/// <summary>
/// Routes pointer input to tool handlers. The tool that receives a press receives every move and the release until no button is held,
/// and a temporary tool engages while a modifier key is held outside of a gesture.
/// </summary>
public sealed class ToolInputRouter<TState>
{
    private readonly Func<IToolHandler<TState>?> _selectedTool;
    private readonly Func<IToolHandler<TState>?> _temporaryTool;
    private readonly IReadOnlyCollection<Key> _modifierKeys;

    private IToolHandler<TState>? _overrideTool;
    private IToolHandler<TState>? _gestureTool;

    /// <param name="selectedTool">The tool the user selected</param>
    /// <param name="temporaryTool">The tool a modifier or Alt engages, or null when there is none</param>
    /// <param name="modifierKeys">Keys that engage <paramref name="temporaryTool"/> while held</param>
    public ToolInputRouter(Func<IToolHandler<TState>?> selectedTool, Func<IToolHandler<TState>?> temporaryTool, IReadOnlyCollection<Key> modifierKeys)
    {
        _selectedTool = selectedTool;
        _temporaryTool = temporaryTool;
        _modifierKeys = modifierKeys;
    }

    public bool IsOverrideEngaged => _overrideTool is not null;
    public bool IsGestureActive => _gestureTool is not null;

    /// <summary>
    /// The tool that keys are dispatched to: the engaged temporary tool, otherwise the selected tool
    /// </summary>
    public IToolHandler<TState>? KeyTarget => _overrideTool ?? _selectedTool();

    /// <summary>
    /// The tool that pointer input with <paramref name="modifiers"/> goes to
    /// </summary>
    public IToolHandler<TState>? Resolve(KeyModifiers modifiers)
    {
        if (_gestureTool is not null)
            return _gestureTool;

        if (_overrideTool is not null)
            return _overrideTool;

        if (modifiers.HasFlag(KeyModifiers.Alt) && _temporaryTool() is { } temporary)
            return temporary;

        return _selectedTool();
    }

    public ToolResult Press(ToolContext ctx, TState state)
    {
        _gestureTool = Resolve(ctx.MouseState.Modifiers);
        return _gestureTool?.OnMouseDown(ctx, state) ?? default;
    }

    public ToolResult Move(ToolContext ctx, TState state)
    {
        // A move with no button held means the release went elsewhere, such as to a drag and drop
        var released = _gestureTool is not null && !IsAnyButtonPressed(ctx.MouseState) ? Release(ctx, state) : default;

        var result = Resolve(ctx.MouseState.Modifiers)?.OnMouseMove(ctx, state) ?? default;
        return result with { Invalidation = (InvalidationLevel)Math.Max((int)result.Invalidation, (int)released.Invalidation) };
    }

    public ToolResult Release(ToolContext ctx, TState state)
    {
        var tool = Resolve(ctx.MouseState.Modifiers);
        var result = tool?.OnMouseUp(ctx, state) ?? default;

        if (!IsAnyButtonPressed(ctx.MouseState))
        {
            _gestureTool = null;

            if (_overrideTool is null && (ctx.MouseState.Modifiers & (KeyModifiers.Control | KeyModifiers.Shift)) != 0)
                _overrideTool = _temporaryTool();
        }

        return result;
    }

    /// <returns>True when the temporary tool was engaged</returns>
    public bool KeyDown(Key key)
    {
        if (_overrideTool is not null || _gestureTool is not null || !_modifierKeys.Contains(key))
            return false;

        _overrideTool = _temporaryTool();
        return _overrideTool is not null;
    }

    /// <returns>True when the temporary tool was released</returns>
    public bool KeyUp(Key key)
    {
        if (_overrideTool is null || !_modifierKeys.Contains(key))
            return false;

        _overrideTool = null;
        return true;
    }

    /// <summary>
    /// Ends any gesture in progress, such as when the pointer leaves, and deactivates the tool it was going to
    /// </summary>
    /// <returns>The history action the deactivated tool finalized, if any</returns>
    public HistoryAction? EndGesture(TState state)
    {
        var tool = _gestureTool ?? KeyTarget;
        _gestureTool = null;
        return tool?.Deactivate(state);
    }

    /// <summary>
    /// Ends any gesture in progress and releases the temporary tool, such as when the edit mode changes
    /// </summary>
    /// <returns>The history action the gesture's tool finalized, if any</returns>
    public HistoryAction? Reset(TState state)
    {
        var tool = _gestureTool;
        _gestureTool = null;
        _overrideTool = null;
        return tool?.Deactivate(state);
    }

    private static bool IsAnyButtonPressed(MouseState mouseState) =>
        mouseState.LeftButtonPressed || mouseState.MiddleButtonPressed || mouseState.RightButtonPressed;
}
