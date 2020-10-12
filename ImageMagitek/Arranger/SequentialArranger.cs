using System;
using System.Collections.Generic;
using System.Drawing;
using ImageMagitek.Project;
using ImageMagitek.Codec;
using ImageMagitek.Colors;
using System.Linq;

namespace ImageMagitek
{
    public class SequentialArranger : Arranger
    {
        /// <summary>
        /// Gets the filesize of the file associated with a SequentialArranger
        /// </summary>
        public long FileSize { get; protected set; }

        /// <summary>
        /// Gets the current file address of the file associated with a SequentialArranger
        /// </summary>
        public long FileAddress { get; protected set; }

        /// <summary>
        /// Number of bits required to be read from file sequentially to fully display the Arranger
        /// </summary>
        public long ArrangerBitSize { get; protected set; }
        public override bool ShouldBeSerialized { get; set; } = true;

        /// <summary>
        /// Codec that is assigned to each ArrangerElement
        /// </summary>
        public IGraphicsCodec ActiveCodec { get; protected set; }

        /// <summary>
        /// DataFile that is assigned to each ArrangerElement
        /// </summary>
        public DataFile ActiveDataFile { get; protected set; }

        /// <summary>
        /// Palette that is assigned to each ArrangerElement
        /// </summary>
        public Palette ActivePalette { get; protected set; }

        private ICodecFactory _codecs;

        /// <summary>
        /// Constructs a new SequentialArranger
        /// </summary>
        /// <param name="arrangerWidth">Width of arranger in elements</param>
        /// <param name="arrangerHeight">Height of arranger in elements</param>
        /// <param name="dataFile">DataFile assigned to each Element</param>
        /// <param name="palette">Palette assigned to each Element</param>
        /// <param name="codecFactory">Factory responsible for creating new codecs</param>
        /// <param name="codecName">Name of codec each Element will be initialized to</param>
        public SequentialArranger(int arrangerWidth, int arrangerHeight, DataFile dataFile, Palette palette, ICodecFactory codecFactory, string codecName)
        {
            Mode = ArrangerMode.Sequential;
            FileSize = dataFile.Stream.Length;
            Name = dataFile.Name;
            ActiveDataFile = dataFile;
            ActivePalette = palette;
            _codecs = codecFactory;

            ActiveCodec = _codecs.GetCodec(codecName);
            ColorType = ActiveCodec.ColorType;

            ElementPixelSize = new Size(ActiveCodec.Width, ActiveCodec.Height);

            Resize(arrangerWidth, arrangerHeight);
        }

        /// <summary>
        /// Moves the sequential arranger to the specified address
        /// If the arranger will overflow the file, then seek only to the furthest offset
        /// </summary>
        /// <param name="absoluteAddress">Specified address to move the arranger to</param>
        /// <returns></returns>
        public FileBitAddress Move(FileBitAddress absoluteAddress)
        {
            if (Mode != ArrangerMode.Sequential)
                throw new InvalidOperationException($"{nameof(Move)}: Arranger {Name} is not in sequential mode");

            FileBitAddress address;
            FileBitAddress testaddress = absoluteAddress + ArrangerBitSize; // Tests the bounds of the arranger vs the file size

            if (FileSize * 8 < ArrangerBitSize) // Arranger needs more bits than the entire file
                address = new FileBitAddress(0, 0);
            else if (testaddress.Bits() > FileSize * 8)
                address = new FileBitAddress(FileSize * 8 - ArrangerBitSize);
            else
                address = absoluteAddress;

            int ElementStorageSize = ActiveCodec.StorageSize;

            for (int y = 0; y < ArrangerElementSize.Height; y++)
            {
                for (int x = 0; x < ArrangerElementSize.Width; x++)
                {
                    var el = GetElement(x, y);
                    if (el is ArrangerElement element)
                    {
                        element = element.WithAddress(address);
                        SetElement(element, x, y);
                        address += ElementStorageSize;
                    }
                }
            }

            FileAddress = GetInitialSequentialFileAddress().FileOffset;

            return new FileBitAddress(FileAddress, 0);
        }

        /// <summary>
        /// Resizes a SequentialArranger to the specified number of elements
        /// </summary>
        /// <param name="arrangerWidth">Width of Arranger in Elements</param>
        /// <param name="arrangerHeight">Height of Arranger in Elements</param>
        public override void Resize(int arrangerWidth, int arrangerHeight)
        {
            if (Mode != ArrangerMode.Sequential)
                throw new InvalidOperationException($"{nameof(Resize)} property '{nameof(Mode)}' is in invalid {nameof(ArrangerMode)} ({Mode.ToString()})");

            FileBitAddress address = FileAddress;

            ElementPixelSize = new Size(ActiveCodec.Width, ActiveCodec.Height);

            if (ElementGrid is null) // New Arranger being initially sized
            {
                ElementGrid = new ArrangerElement?[arrangerWidth, arrangerHeight];
            }
            else // Arranger being resized with existing elements
            {
                var oldElementGrid = ElementGrid;
                ElementGrid = new ArrangerElement?[arrangerWidth, arrangerHeight];
                var elemsX = Math.Min(ArrangerElementSize.Width, arrangerWidth);
                var elemsY = Math.Min(ArrangerElementSize.Height, arrangerHeight);

                for (int i = 0; i < elemsY; i++)
                    for (int j = 0; j < elemsX; j++)
                        ElementGrid[j, i] = oldElementGrid[j, i];

                address = GetInitialSequentialFileAddress();
            }

            ArrangerElementSize = new Size(arrangerWidth, arrangerHeight);
            ArrangerBitSize = arrangerWidth * arrangerHeight * ActiveCodec.StorageSize;

            for (int posY = 0; posY < arrangerHeight; posY++)
            {
                for (int posX = 0; posX < arrangerWidth; posX++)
                {
                    var el = new ArrangerElement(posX * ElementPixelSize.Width, posY * ElementPixelSize.Height, ActiveDataFile, address, ActiveCodec, ActivePalette);

                    if (el.Codec.Layout == ImageLayout.Tiled)
                        address += ActiveCodec.StorageSize;
                    else if (el.Codec.Layout == ImageLayout.Single)
                        address += (ElementPixelSize.Width + ActiveCodec.RowStride) * ActiveCodec.ColorDepth / 4; // TODO: Fix sequential arranger offsets to be bit-wise
                    else
                        throw new NotSupportedException();

                    SetElement(el, posX, posY);
                }
            }

            address = GetInitialSequentialFileAddress();
            address = Move(address);
        }

        /// <summary>
        /// Sets Element to a position in the Arranger ElementGrid using a shallow copy
        /// </summary>
        /// <param name="element">Element to be placed into the ElementGrid</param>
        /// <param name="posX">x-coordinate in Element coordinates</param>
        /// <param name="posY">y-coordinate in Element coordinates</param>
        public override void SetElement(in ArrangerElement? element, int posX, int posY)
        {
            if (ElementGrid is null)
                throw new NullReferenceException($"{nameof(SetElement)} property '{nameof(ElementGrid)}' was null");

            if (posX > ArrangerElementSize.Width || posY > ArrangerElementSize.Height)
                throw new ArgumentOutOfRangeException($"{nameof(SetElement)} parameter was out of range: ({posX}, {posY})");

            if (element is ArrangerElement)
            {
                if (element?.Codec.ColorType != ColorType || element?.Codec.Name != ActiveCodec.Name)
                    throw new ArgumentException($"{nameof(SetElement)} parameter '{nameof(element)}' cannot be assigned to SequentialArranger '{Name}'");

                ElementGrid[posX, posY] = element;
            }
            else
                ElementGrid[posX, posY] = element;
        }

        /// <summary>
        /// Changes each Element's codec
        /// </summary>
        /// <param name="codec">New codec</param>
        public void ChangeCodec(IGraphicsCodec codec) => ChangeCodec(codec, ArrangerElementSize.Width, ArrangerElementSize.Height);

        /// <summary>
        /// Changes each Element's codec and resizes the Arranger accordingly
        /// </summary>
        /// <param name="codec">New codec</param>
        /// <param name="arrangerWidth">Arranger Width in Elements</param>
        /// <param name="arrangerHeight">Arranger Height in Elements</param>
        public void ChangeCodec(IGraphicsCodec codec, int arrangerWidth, int arrangerHeight)
        {
            FileBitAddress address = GetInitialSequentialFileAddress();
            ElementPixelSize = new Size(codec.Width, codec.Height);

            ActiveCodec = codec;
            ColorType = ActiveCodec.ColorType;

            if (codec.Layout == ImageLayout.Single)
                Layout = ArrangerLayout.Single;
            else if (codec.Layout == ImageLayout.Tiled)
                Layout = ArrangerLayout.Tiled;

            if (ArrangerElementSize.Width != arrangerWidth || ArrangerElementSize.Height != arrangerHeight)
                Resize(arrangerWidth, arrangerHeight);

            ArrangerBitSize = ArrangerElementSize.Width * ArrangerElementSize.Height * codec.StorageSize;

            int elY = 0;

            for (int posY = 0; posY < ArrangerElementSize.Height; posY++)
            {
                int elX = 0;
                for (int posX = 0; posX < ArrangerElementSize.Width; posX++)
                {
                    if (GetElement(posX, posY) is ArrangerElement el)
                    {
                        el = new ArrangerElement(elX, elY, el.DataFile, address, _codecs.CloneCodec(ActiveCodec), el.Palette);
                        SetElement(el, posX, posY);

                        if (ActiveCodec.Layout == ImageLayout.Tiled)
                            address += ActiveCodec.StorageSize;
                        else if (ActiveCodec.Layout == ImageLayout.Single)
                            address += (ElementPixelSize.Width + ActiveCodec.RowStride) * ActiveCodec.ColorDepth / 4; // TODO: Fix sequential arranger offsets to be bit-wise
                        else
                            throw new NotSupportedException();
                    }

                    elX += ElementPixelSize.Width;
                }
                elY += ElementPixelSize.Height;
            }

            address = GetInitialSequentialFileAddress();
            this.Move(address);
        }

        /// <summary>
        /// Changes each element's palette to the provided palette
        /// </summary>
        /// <param name="pal">New palette</param>
        public void ChangePalette(Palette pal)
        {
            ActivePalette = pal;
            for (int y = 0; y < ArrangerElementSize.Height; y++)
            {
                for (int x = 0; x < ArrangerElementSize.Width; x++)
                {
                    if (GetElement(x, y) is ArrangerElement el)
                    {
                        el = el.WithPalette(pal);
                        SetElement(el, x, y);
                    }
                }
            }
        }

        /// <summary>
        /// Private method for cloning an Arranger
        /// </summary>
        /// <param name="posX">Left edge of Arranger in pixel coordinates</param>
        /// <param name="posY">Top edge of Arranger in pixel coordinates</param>
        /// <param name="width">Width of Arranger in pixels</param>
        /// <param name="height">Height of Arranger in pixels</param>
        /// <returns></returns>
        protected override Arranger CloneArrangerCore(int posX, int posY, int width, int height)
        {
            var elemX = posX / ElementPixelSize.Width;
            var elemY = posY / ElementPixelSize.Height;
            var elemsWidth = (width + ElementPixelSize.Width - 1) / ElementPixelSize.Width;
            var elemsHeight = (height + ElementPixelSize.Height - 1) / ElementPixelSize.Height;

            var arranger = new ScatteredArranger(Name, ColorType, Layout, elemsWidth, elemsHeight, ElementPixelSize.Width, ElementPixelSize.Height);

            for (int y = 0; y < elemsHeight; y++)
            {
                for (int x = 0; x < elemsWidth; x++)
                {
                    var elX = x * ElementPixelSize.Width;
                    var elY = y * ElementPixelSize.Height;
                    if (ElementGrid[x + elemX, y + elemY] is ArrangerElement el)
                    {
                        var codec = _codecs.CloneCodec(el.Codec);

                        el = el.WithCodec(codec, elX, elY);
                        arranger.SetElement(el, x, y);
                    }
                }
            }

            return arranger;
        }

        /// <summary>
        /// Gets the initial file address of a Sequential Arranger
        /// </summary>
        /// <returns></returns>
        public FileBitAddress GetInitialSequentialFileAddress()
        {
            if (ElementGrid is null)
                throw new NullReferenceException($"{nameof(GetInitialSequentialFileAddress)} property '{nameof(ElementGrid)}' was null");

            if (Mode != ArrangerMode.Sequential)
                throw new InvalidOperationException($"{nameof(GetInitialSequentialFileAddress)} property '{nameof(Mode)}' " + 
                    $"is in invalid {nameof(ArrangerMode)} ({Mode})");

            return ElementGrid[0, 0]?.FileAddress ?? 0;
        }

        /// <summary>
        /// Gets the GraphicsFormat name for a Sequential Arranger
        /// </summary>
        /// <returns></returns>
        public string GetSequentialGraphicsFormat() => ActiveCodec.Name;

        public override IEnumerable<IProjectResource> LinkedResources
        {
            get
            {
                var set = new HashSet<IProjectResource>();

                foreach (var el in EnumerateElements().OfType<ArrangerElement>())
                {
                    if (el.Palette is object)
                        set.Add(el.Palette);

                    if (el.DataFile is object)
                        set.Add(el.DataFile);
                }

                return set;
            }
        }
    }
}
