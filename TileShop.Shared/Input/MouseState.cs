namespace TileShop.Shared.Input;

public readonly struct MouseState
{
    public bool LeftButtonPressed { get; }
    public bool MiddleButtonPressed { get; }
    public bool RightButtonPressed { get; }
    public KeyModifiers Modifiers { get; }

    public MouseState(bool leftButtonPressed, bool middleButtonPressed, bool rightButtonPressed, KeyModifiers modifiers)
    {
        LeftButtonPressed = leftButtonPressed;
        MiddleButtonPressed = middleButtonPressed;
        RightButtonPressed = rightButtonPressed;
        Modifiers = modifiers;
    }
}
