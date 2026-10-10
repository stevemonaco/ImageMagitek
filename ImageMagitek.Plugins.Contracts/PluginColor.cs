namespace ImageMagitek.Plugins;

/// <summary>
/// A 32-bit color with 8-bit red, green, blue and alpha channels, in that memory order.
/// </summary>
public readonly record struct PluginColor(byte R, byte G, byte B, byte A);
