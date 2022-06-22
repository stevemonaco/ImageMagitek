namespace TileShop.Shared.Input;

public interface IStateDriver
{
    public void MouseMove(double x, double y, MouseState mouseState);
    public void MouseEnter();
    public void MouseLeave();
    public void MouseDown(double x, double y, MouseState mouseState);
    public void MouseUp(double x, double y, MouseState mouseState);
    public void MouseWheel(MouseWheelDirection direction, KeyModifiers modifiers);

    public void KeyPress(KeyState keyState);
}
