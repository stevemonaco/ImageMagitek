using System.Collections.Generic;
using Avalonia.Input;
using TileShop.Shared.Input;

using Key = TileShop.Shared.Input.Key;
using KeyModifiers = TileShop.Shared.Input.KeyModifiers;
using AvKey = Avalonia.Input.Key;
using AvKeyModifiers = Avalonia.Input.KeyModifiers;

namespace TileShop.AvaloniaUI.Input;
public static class InputAdapter
{
    public static MouseState CreateMouseState(PointerPoint point, AvKeyModifiers keys)
    {
        var properties = point.Properties;
        var modifiers = CreateKeyModifiers(keys);

        return new MouseState(properties.IsLeftButtonPressed, properties.IsMiddleButtonPressed, properties.IsRightButtonPressed, modifiers);
    }

    public static KeyState CreateKeyState(AvKey inputKey, AvKeyModifiers keyModifiers)
    {
        var modifiers = CreateKeyModifiers(keyModifiers);

        if (_keyMap.TryGetValue(inputKey, out var key))
        {
            if (modifiers.HasFlag(KeyModifiers.Shift) && _shiftKeyMap.TryGetValue(key, out var shiftKey))
            {
                return new KeyState(shiftKey, modifiers);
            }
            else
            {
                return new KeyState(key, modifiers);
            }
        }

        return new KeyState(Key.None, modifiers);
    }

    public static KeyModifiers CreateKeyModifiers(AvKeyModifiers keys)
    {
        var modifiers = KeyModifiers.None;
        if (keys.HasFlag(AvKeyModifiers.Alt))
            modifiers |= KeyModifiers.Alt;

        if (keys.HasFlag(AvKeyModifiers.Control))
            modifiers |= KeyModifiers.Control;

        if (keys.HasFlag(AvKeyModifiers.Shift))
            modifiers |= KeyModifiers.Shift;

        return modifiers;
    }

    private static Dictionary<AvKey, Key> _keyMap = new()
    {
        [AvKey.A] = Key.A, [AvKey.B] = Key.B, [AvKey.C] = Key.C, [AvKey.D] = Key.D, [AvKey.E] = Key.E,
        [AvKey.F] = Key.F, [AvKey.G] = Key.G, [AvKey.H] = Key.H, [AvKey.I] = Key.I, [AvKey.J] = Key.J,
        [AvKey.K] = Key.K, [AvKey.L] = Key.L, [AvKey.M] = Key.M, [AvKey.N] = Key.N, [AvKey.O] = Key.O,
        [AvKey.P] = Key.P, [AvKey.Q] = Key.Q, [AvKey.R] = Key.R, [AvKey.S] = Key.S, [AvKey.T] = Key.T,
        [AvKey.U] = Key.U, [AvKey.V] = Key.V, [AvKey.W] = Key.W, [AvKey.X] = Key.X, [AvKey.Y] = Key.Y,
        [AvKey.Z] = Key.Z, [AvKey.D0] = Key.Digit0, [AvKey.D1] = Key.Digit1, [AvKey.D2] = Key.Digit2, [AvKey.D3] = Key.Digit3,
        [AvKey.D4] = Key.Digit4, [AvKey.D5] = Key.Digit5, [AvKey.D6] = Key.Digit6, [AvKey.D7] = Key.Digit7, [AvKey.D8] = Key.Digit8,
        [AvKey.D9] = Key.Digit9, [AvKey.OemPeriod] = Key.Period, [AvKey.OemComma] = Key.Comma, [AvKey.OemQuestion] = Key.ForwardSlash, [AvKey.OemTilde] = Key.Backtick,
        [AvKey.OemMinus] = Key.Minus, [AvKey.OemPlus] = Key.Equal, [AvKey.Back] = Key.Backspace, [AvKey.OemOpenBrackets] = Key.OpenBracket, [AvKey.OemCloseBrackets] = Key.CloseBracket,
        [AvKey.OemBackslash] = Key.BackSlash, [AvKey.OemPipe] = Key.BackSlash, [AvKey.OemSemicolon] = Key.Semicolon, [AvKey.OemQuotes] = Key.Apostrophe,
        [AvKey.Delete] = Key.Delete, [AvKey.Home] = Key.Home, [AvKey.End] = Key.End, [AvKey.PageUp] = Key.PageUp, [AvKey.PageDown] = Key.PageDown,
        [AvKey.Up] = Key.Up, [AvKey.Down] = Key.Down, [AvKey.Left] = Key.Left, [AvKey.Right] = Key.Right, [AvKey.Tab] = Key.Tab,
        [AvKey.LeftShift] = Key.LeftShift, [AvKey.RightShift] = Key.RightShift, [AvKey.LeftCtrl] = Key.LeftControl, [AvKey.RightCtrl] = Key.RightControl, [AvKey.LeftAlt] = Key.LeftAlt,
        [AvKey.RightAlt] = Key.RightAlt, [AvKey.Enter] = Key.Enter, [AvKey.Space] = Key.Space
    };

    private static Dictionary<Key, Key> _shiftKeyMap = new()
    {
        [Key.Digit1] = Key.Exclamation, [Key.Digit2]  = Key.At, [Key.Digit3] = Key.Hash,
        [Key.Digit4] = Key.Dollar, [Key.Digit5] = Key.Percent, [Key.Digit6] = Key.Carat,
        [Key.Digit7] = Key.Ampersand, [Key.Digit8] = Key.Star, [Key.Digit9] = Key.OpenParenthesis,
        [Key.Digit0] = Key.CloseParenthesis, [Key.Minus] = Key.Underscore, [Key.Equal] = Key.Plus,
        [Key.Backtick] = Key.Tilde, [Key.BackSlash] = Key.Pipe, [Key.Period]  = Key.GreaterThan,
        [Key.Comma] = Key.LessThan, [Key.ForwardSlash] = Key.Question, [Key.OpenBracket] = Key.OpenBrace,
        [Key.CloseBracket] = Key.CloseBrace
    };
}
