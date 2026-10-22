namespace ImageMagitek.Project.Serialization;

/// <param name="FileAddress">Byte offset of the run's first color</param>
public sealed record FileColorSourceModel(long FileAddress, int Entries, Endian Endian) : IColorSourceModel;
