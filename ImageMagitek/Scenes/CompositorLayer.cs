using System.Drawing;

namespace ImageMagitek.Scenes;

/// <summary>
/// A layer image placed on the compositor's canvas at a pixel position, drawn only when visible.
/// </summary>
public sealed record CompositorLayer(LayerImage Image, Point Position, bool IsVisible = true);
