using System.Drawing;

namespace TileShop.Shared.Input;

public interface IStateDriver
{
    Point? LastMousePosition { get; }

    void MouseMove(double x, double y, MouseState mouseState);
    void MouseEnter();
    void MouseLeave();
    void MouseDown(double x, double y, MouseState mouseState);
    void MouseUp(double x, double y, MouseState mouseState);
    void MouseWheel(MouseWheelDirection direction, KeyModifiers modifiers);

    void KeyPress(KeyState keyState, double? x, double? y);
}
