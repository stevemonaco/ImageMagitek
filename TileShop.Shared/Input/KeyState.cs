namespace TileShop.Shared.Input;

public readonly struct KeyState
{
    public Key Key { get; }
    public KeyModifiers Modifiers { get; }

    public KeyState(Key key, KeyModifiers modifiers)
    {
        Key = key;
        Modifiers = modifiers;
    }
}
