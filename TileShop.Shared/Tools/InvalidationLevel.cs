using System;

namespace TileShop.Shared.Tools;

[Flags]
public enum InvalidationLevel
{
    None = 0,
    Overlay = 1,
    PixelData = 2,
}
