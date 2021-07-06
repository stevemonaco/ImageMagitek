using ImageMagitek.Codec;
using ImageMagitek.Colors;

namespace ImageMagitek
{
    public enum MirrorOperation { None, Horizontal, Vertical, Both }
    public enum RotationOperation { None, Left, Right, Turn }

    /// <summary>
    /// Contains all necessary data to encode/decode a single element in the arranger
    /// </summary>
    public readonly struct ArrangerElement
    {
        /// <summary>
        /// DataFile which contains the Element's pixel data
        /// </summary>
        public DataFile DataFile { get; }

        /// <summary>
        /// FileAddress of Element
        /// </summary>
        public FileBitAddress FileAddress { get; }

        /// <summary>
        /// Codec used for encoding and decoding
        /// </summary>
        public IGraphicsCodec Codec { get; }

        /// <summary>
        /// Width of Element in pixels
        /// </summary>
        public int Width => Codec.Width;

        /// <summary>
        /// Height of Element in pixels
        /// </summary>
        public int Height => Codec.Height;

        /// <summary>
        /// Palette to apply to the element's pixel data
        /// </summary>
        public Palette Palette { get; }

        /// <summary>
        /// Left edge of the Element within the Arranger in pixel coordinates, inclusive
        /// </summary>
        public int X1 { get; }

        /// <summary>
        /// Top edge of the Element within the Arranger in pixel coordinates, inclusive
        /// </summary>
        public int Y1 { get; }

        /// <summary>
        /// Right edge of the Element within the Arranger in pixel coordinates, inclusive
        /// </summary>
        public int X2 { get => X1 + Width - 1; }

        /// <summary>
        /// Bottom edge of the Element within the Arranger in pixel coordinates, inclusive
        /// </summary>
        public int Y2 { get => Y1 + Height - 1; }

        public MirrorOperation Mirror { get; }

        public RotationOperation Rotation { get; }

        public ArrangerElement(int x1, int y1)
        {
            X1 = x1;
            Y1 = y1;
            FileAddress = new FileBitAddress(0, 0);
            DataFile = null;
            Codec = null;
            Palette = null;
            Mirror = MirrorOperation.None;
            Rotation = RotationOperation.None;
        }

        public ArrangerElement(int x1, int y1, DataFile dataFile, FileBitAddress address, IGraphicsCodec codec, Palette palette)
        {
            X1 = x1;
            Y1 = y1;
            DataFile = dataFile;
            FileAddress = address;
            Codec = codec;
            Palette = palette;
            Mirror = MirrorOperation.None;
            Rotation = RotationOperation.None;
        }

        public ArrangerElement(int x1, int y1, DataFile dataFile, FileBitAddress address, IGraphicsCodec codec, Palette palette,
            MirrorOperation mirror, RotationOperation rotation)
        {
            X1 = x1;
            Y1 = y1;
            DataFile = dataFile;
            FileAddress = address;
            Codec = codec;
            Palette = palette;
            Mirror = mirror;
            Rotation = rotation;
        }

        public ArrangerElement WithLocation(int x1, int y1) =>
            new ArrangerElement(x1, y1, DataFile, FileAddress, Codec, Palette, Mirror, Rotation);

        public ArrangerElement WithFile(DataFile dataFile, FileBitAddress fileAddress) =>
            new ArrangerElement(X1, Y1, dataFile, fileAddress, Codec, Palette, Mirror, Rotation);

        public ArrangerElement WithPalette(Palette palette) =>
            new ArrangerElement(X1, Y1, DataFile, FileAddress, Codec, palette, Mirror, Rotation);

        public ArrangerElement WithAddress(FileBitAddress address) =>
            new ArrangerElement(X1, Y1, DataFile, address, Codec, Palette, Mirror, Rotation);

        public ArrangerElement WithCodec(IGraphicsCodec codec) =>
            new ArrangerElement(X1, Y1, DataFile, FileAddress, codec, Palette, Mirror, Rotation);

        public ArrangerElement WithCodec(IGraphicsCodec codec, int x1, int y1) =>
            new ArrangerElement(x1, y1, DataFile, FileAddress, codec, Palette, Mirror, Rotation);

        public ArrangerElement WithTarget(DataFile dataFile, FileBitAddress fileAddress, IGraphicsCodec codec, Palette palette) =>
            new ArrangerElement(X1, Y1, dataFile, fileAddress, codec, palette, Mirror, Rotation);

        public ArrangerElement WithMirror(MirrorOperation mirror) =>
            new ArrangerElement(X1, Y1, DataFile, FileAddress, Codec, Palette, mirror, Rotation);

        public ArrangerElement WithRotation(RotationOperation rotation) =>
            new ArrangerElement(X1, Y1, DataFile, FileAddress, Codec, Palette, Mirror, rotation);
    }
}
