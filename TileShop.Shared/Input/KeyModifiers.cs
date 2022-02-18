using System;

namespace TileShop.Shared.Input;

[Flags]
public enum KeyModifiers
{
    None            = 0b_0000_0000,
    Ctrl            = 0b_0000_0001,
    Alt             = 0b_0000_0010,
    Shift           = 0b_0000_0100,
    CtrlAlt         = Ctrl | Alt,
    CtrlShift       = Ctrl | Shift,
    AltShift        = Alt | Shift,
    CtrlAltShift    = Ctrl | Alt | Shift
}
