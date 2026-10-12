using System.Collections.Generic;
using System.Drawing;

namespace TileShop.Shared.Input;

public interface IStateDriver
{
    Point? LastMousePosition { get; }

    bool MouseMove(double x, double y, MouseState mouseState, IReadOnlyList<(double X, double Y)> samples);
    bool MouseEnter();
    bool MouseLeave();
    bool MouseDown(double x, double y, MouseState mouseState);
    bool MouseUp(double x, double y, MouseState mouseState);
    bool MouseWheel(MouseWheelDirection direction, KeyModifiers modifiers);

    bool KeyPress(KeyState keyState, double? x, double? y);
}
