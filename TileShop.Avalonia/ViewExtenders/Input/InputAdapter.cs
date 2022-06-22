using Avalonia.Input;
using TileShop.Shared.Input;

using KeyModifiers = TileShop.Shared.Input.KeyModifiers;

namespace TileShop.AvaloniaUI.Input;
public static class InputAdapter
{
    public static MouseState CreateMouseState(PointerPoint point, Avalonia.Input.KeyModifiers keys)
    {
        var properties = point.Properties;
        var modifiers = CreateKeyModifiers(keys);

        return new MouseState(properties.IsLeftButtonPressed, properties.IsMiddleButtonPressed, properties.IsRightButtonPressed, modifiers);
    }

    public static KeyModifiers CreateKeyModifiers(Avalonia.Input.KeyModifiers keys)
    {
        var modifiers = KeyModifiers.None;
        if (keys.HasFlag(Avalonia.Input.KeyModifiers.Alt))
            modifiers |= KeyModifiers.Alt;

        if (keys.HasFlag(Avalonia.Input.KeyModifiers.Control))
            modifiers |= KeyModifiers.Ctrl;

        if (keys.HasFlag(Avalonia.Input.KeyModifiers.Shift))
            modifiers = KeyModifiers.Shift;

        return modifiers;
    }
}
