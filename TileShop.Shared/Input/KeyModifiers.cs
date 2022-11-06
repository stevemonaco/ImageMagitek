using System;

namespace TileShop.Shared.Input;

[Flags]
public enum KeyModifiers
{
    None            = 0b_0000_0000,
    Control         = 0b_0000_0001,
    Alt             = 0b_0000_0010,
    Shift           = 0b_0000_0100,
    ControlAlt      = Control | Alt,
    ControlShift    = Control | Shift,
    AltShift        = Alt | Shift,
    ControlAltShift = Control | Alt | Shift
}
