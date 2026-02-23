using TileShop.Shared.Input;

namespace TileShop.Shared.Tools;

public readonly struct ToolContext
{
    /// <summary>Raw X coordinate (sub-pixel precision).</summary>
    public double X { get; }

    /// <summary>Raw Y coordinate (sub-pixel precision).</summary>
    public double Y { get; }

    /// <summary>X coordinate clamped to valid pixel range.</summary>
    public int PixelX { get; }

    /// <summary>Y coordinate clamped to valid pixel range.</summary>
    public int PixelY { get; }

    public MouseState MouseState { get; }
    public KeyState KeyState { get; }

    public ToolContext(double x, double y, int pixelX, int pixelY, MouseState mouseState)
    {
        X = x;
        Y = y;
        PixelX = pixelX;
        PixelY = pixelY;
        MouseState = mouseState;
        KeyState = default;
    }

    public ToolContext(double x, double y, int pixelX, int pixelY, KeyState keyState)
    {
        X = x;
        Y = y;
        PixelX = pixelX;
        PixelY = pixelY;
        MouseState = default;
        KeyState = keyState;
    }
}
