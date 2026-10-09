using System.Collections.Generic;
using System.Drawing;
using TileShop.Shared.Input;
using TileShop.Shared.Models;
using TileShop.Shared.Tools;
using Xunit;

namespace ImageMagitek.UnitTests.Tools;

public class ToolInputRouterTests
{
    private readonly RecordingTool _selected = new();
    private readonly RecordingTool _temporary = new();
    private readonly ToolInputRouter<object> _router;
    private readonly object _state = new();

    public ToolInputRouterTests()
    {
        _router = new ToolInputRouter<object>(() => _selected, () => _temporary, [Key.LeftControl, Key.RightControl, Key.LeftShift, Key.RightShift]);
    }

    [Fact]
    public void ModifierDuringPress_ReleaseGoesToPressTool()
    {
        _router.Press(Pointer(left: true), _state);

        Assert.False(_router.KeyDown(Key.LeftControl));
        _router.Move(Pointer(left: true, KeyModifiers.Control), _state);
        Assert.False(_router.KeyUp(Key.LeftControl));
        _router.Release(Pointer(left: false), _state);

        Assert.Equal(["Down", "Move", "Up"], _selected.Calls);
        Assert.Empty(_temporary.Calls);
        Assert.False(_router.IsOverrideEngaged);
    }

    [Fact]
    public void ModifierHeldAtRelease_EngagesTemporaryToolAfter()
    {
        _router.Press(Pointer(left: true), _state);
        Assert.False(_router.KeyDown(Key.LeftShift));

        _router.Release(Pointer(left: false, KeyModifiers.Shift), _state);

        Assert.True(_router.IsOverrideEngaged);
        Assert.Equal(["Down", "Up"], _selected.Calls);

        _router.Press(Pointer(left: true, KeyModifiers.Shift), _state);
        Assert.Equal(["Down"], _temporary.Calls);
    }

    [Fact]
    public void AltOnMoveDuringPress_StaysWithPressTool()
    {
        _router.Press(Pointer(left: true), _state);
        _router.Move(Pointer(left: true, KeyModifiers.Alt), _state);
        _router.Release(Pointer(left: false, KeyModifiers.Alt), _state);

        Assert.Equal(["Down", "Move", "Up"], _selected.Calls);
        Assert.Empty(_temporary.Calls);
        Assert.False(_router.IsOverrideEngaged);
    }

    [Fact]
    public void ModifierWithoutPress_EngagesImmediately()
    {
        Assert.True(_router.KeyDown(Key.RightControl));
        Assert.True(_router.IsOverrideEngaged);

        _router.Press(Pointer(left: true, KeyModifiers.Control), _state);
        _router.Release(Pointer(left: false, KeyModifiers.Control), _state);

        Assert.Equal(["Down", "Up"], _temporary.Calls);
        Assert.Empty(_selected.Calls);

        Assert.True(_router.KeyUp(Key.RightControl));
        Assert.False(_router.IsOverrideEngaged);
    }

    private static ToolContext Pointer(bool left, KeyModifiers modifiers = KeyModifiers.None) =>
        new(1, 1, 1, 1, new MouseState(left, false, false, modifiers));

    private sealed class RecordingTool : IToolHandler<object>
    {
        public List<string> Calls { get; } = [];

        public ToolCursor Cursor => ToolCursor.Default;

        public ToolResult OnMouseDown(ToolContext ctx, object state) => Record("Down");
        public ToolResult OnMouseMove(ToolContext ctx, object state) => Record("Move");
        public ToolResult OnMouseUp(ToolContext ctx, object state) => Record("Up");
        public ToolResult OnKeyDown(ToolContext ctx, object state) => ToolResult.Unhandled;
        public ToolResult OnKeyUp(ToolContext ctx, object state) => ToolResult.Unhandled;
        public Rectangle? GetTargetRect(ToolContext ctx, object state) => null;
        public HistoryAction? Deactivate(object state) => null;

        private ToolResult Record(string call)
        {
            Calls.Add(call);
            return ToolResult.HandledNoInvalidation;
        }
    }
}
