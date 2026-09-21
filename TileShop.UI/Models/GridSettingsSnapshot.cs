using Avalonia.Media;

namespace TileShop.UI.Models;

/// <summary>
/// User-editable grid settings, captured as a value so they can be previewed and restored
/// </summary>
public sealed record GridSettingsSnapshot(
    int WidthSpacing,
    int HeightSpacing,
    int OriginX,
    int OriginY,
    Color LineColor,
    Color PrimaryColor,
    Color SecondaryColor);
