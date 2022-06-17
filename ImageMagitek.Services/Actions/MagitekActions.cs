using System.Collections.Generic;
using System.Drawing;
using ImageMagitek.Colors;

namespace ImageMagitek.Services.Actions;

public interface IMagitekAction
{
}

public record PencilAction<TColor>(TColor PencilColor, IReadOnlyCollection<Point> ModifiedPoints) : IMagitekAction
    where TColor : struct;

public record FloodFillTestAction<TColor>(TColor FillColor, int X, int Y) : IMagitekAction
    where TColor : struct;

public record ApplyPaletteAction(Palette Palette, IReadOnlyCollection<Point> ModifiedElements);
public record DeleteElementSelectionAction(Rectangle Rect) : IMagitekAction;
public record DeleteElementAction(int ElementX, int ElementY) : IMagitekAction;

public record ResizeArrangerAction(int Width, int Height) : IMagitekAction;
public record MirrorElementAction(int ElementX, int ElementY, MirrorOperation Mirror) : IMagitekAction;
public record RotateElementAction(int ElementX, int ElementY, RotationOperation Rotation) : IMagitekAction;