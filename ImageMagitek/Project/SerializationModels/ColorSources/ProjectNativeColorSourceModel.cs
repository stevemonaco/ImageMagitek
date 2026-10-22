using ImageMagitek.Colors;

namespace ImageMagitek.Project.Serialization;

public sealed record ProjectNativeColorSourceModel(ColorRgba32 Value) : IColorSourceModel;
