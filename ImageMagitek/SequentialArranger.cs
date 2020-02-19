using System;
using System.Collections.Generic;
using System.Drawing;
using ImageMagitek.Project;
using ImageMagitek.Codec;
using ImageMagitek.Colors;

namespace ImageMagitek
{
    public class SequentialArranger : Arranger
    {
        /// <summary>
        /// Gets the filesize of the file associated with a SequentialArranger
        /// </summary>
        public long FileSize { get; private set; }

        /// <summary>
        /// Gets the current file address of the file associated with a SequentialArranger
        /// </summary>
        public long FileAddress { get; private set; }

        /// <summary>
        /// Number of bits required to be read from file sequentially to fully display the Arranger
        /// </summary>
        public long ArrangerBitSize { get; private set; }
        public override bool ShouldBeSerialized { get; set; } = true;

        /// <summary>
        /// Codec that is assigned to each ArrangerElement
        /// </summary>
        public IGraphicsCodec ActiveCodec { get; private set; }

        /// <summary>
        /// DataFile that is assigned to each ArrangerElement
        /// </summary>
        public DataFile ActiveDataFile { get; set; }

        private ICodecFactory _codecs;

        /// <summary>
        /// Constructs a new SequentialArranger
        /// </summary>
        /// <param name="arrangerWidth">Width of arranger in elements</param>
        /// <param name="arrangerHeight">Height of arranger in elements</param>
        /// <param name="dataFile">DataFile that each Element will be initialized with</param>
        /// <param name="codecFactory">Factory responsible for creating new codecs</param>
        /// <param name="codecName">Name of codec each Element will be initialized to</param>
        public SequentialArranger(int arrangerWidth, int arrangerHeight, DataFile dataFile, ICodecFactory codecFactory, string codecName)
        {
            Mode = ArrangerMode.Sequential;
            FileSize = dataFile.Stream.Length;
            Name = dataFile.Name;
            ActiveDataFile = dataFile;
            _codecs = codecFactory;

            ActiveCodec = _codecs.GetCodec(codecName);

            Resize(arrangerWidth, arrangerHeight, dataFile);
        }

        /// <summary>
        /// Resizes a Sequential Arranger to the specified number of Elements and repopulates Element data
        /// </summary>
        /// <param name="arrangerWidth">Width of Arranger in Elements</param>
        /// <param name="arrangerHeight">Height of Arranger in Elements</param>
        /// <returns></returns>
        public override void Resize(int arrangerWidth, int arrangerHeight)
        {
            if (Mode != ArrangerMode.Sequential)
                throw new InvalidOperationException($"{nameof(Resize)} property '{nameof(Mode)}' is in invalid {nameof(ArrangerMode)} ({Mode.ToString()})");

            Resize(arrangerWidth, arrangerHeight, ElementGrid[0, 0].DataFile);
        }

        /// <summary>
        /// Resizes a Sequential Arranger with a new number of elements
        /// </summary>
        /// <param name="arrangerWidth">Width of Arranger in Elements</param>
        /// <param name="arrangerHeight">Height of Arranger in Elements</param>
        /// <param name="dataFileKey">DataFile key in IResourceManager</param>
        /// <param name="codecName">Codec name for encoding/decoding Elements</param>
        /// <returns></returns>
        private FileBitAddress Resize(int arrangerWidth, int arrangerHeight, DataFile dataFile)
        {
            if (Mode != ArrangerMode.Sequential)
                throw new InvalidOperationException($"{nameof(Resize)} property '{nameof(Mode)}' is in invalid {nameof(ArrangerMode)} ({Mode.ToString()})");

            FileBitAddress address;

            ElementPixelSize = new Size(ActiveCodec.Width, ActiveCodec.Height);

            if (ElementGrid is null) // New Arranger being initially sized
            {
                ElementGrid = new ArrangerElement[arrangerWidth, arrangerHeight];
                address = 0;
            }
            else // Arranger being resized with existing elements
            {
                var oldElementGrid = ElementGrid;
                ElementGrid = new ArrangerElement[arrangerWidth, arrangerHeight];
                var elemsX = Math.Min(ArrangerElementSize.Width, arrangerWidth);
                var elemsY = Math.Min(ArrangerElementSize.Height, arrangerHeight);

                for (int i = 0; i < elemsY; i++)
                    for (int j = 0; j < elemsX; j++)
                        ElementGrid[j, i] = oldElementGrid[j, i];

                address = GetInitialSequentialFileAddress();
            }

            ArrangerElementSize = new Size(arrangerWidth, arrangerHeight);
            ArrangerBitSize = arrangerWidth * arrangerHeight * ActiveCodec.StorageSize;

            int elY = 0;

            for (int posY = 0; posY < arrangerHeight; posY++)
            {
                int elX = 0;
                for (int posX = 0; posX < arrangerWidth; posX++)
                {
                    ArrangerElement el = ElementGrid[posX, posY] ??
                        new ArrangerElement(elX, elY, dataFile, address, _codecs.CloneCodec(ActiveCodec), null);

                    SetElement(el, posX, posY);

                    if (el.Codec.Layout == ImageLayout.Tiled)
                        address += ActiveCodec.StorageSize;
                    else if (el.Codec.Layout == ImageLayout.Single)
                        address += (ElementPixelSize.Width + ActiveCodec.RowStride) * ActiveCodec.ColorDepth / 4; // TODO: Fix sequential arranger offsets to be bit-wise
                    else
                        throw new NotSupportedException();

                    elX += ElementPixelSize.Width;
                }
                elY += ElementPixelSize.Height;
            }

            address = GetInitialSequentialFileAddress();
            address = this.Move(address);

            return address;
        }

        /// <summary>
        /// Sets Element to a position in the Arranger ElementGrid using a shallow copy
        /// </summary>
        /// <param name="element">Element to be placed into the ElementGrid</param>
        /// <param name="posX">x-coordinate in Element coordinates</param>
        /// <param name="posY">y-coordinate in Element coordinates</param>
        public override void SetElement(ArrangerElement element, int posX, int posY)
        {
            if (ElementGrid is null)
                throw new NullReferenceException($"{nameof(SetElement)} property '{nameof(ElementGrid)}' was null");

            if (posX > ArrangerElementSize.Width || posY > ArrangerElementSize.Height)
                throw new ArgumentOutOfRangeException($"{nameof(SetElement)} parameter was out of range: ({posX}, {posY})");

            if (element.Codec != null)
                if (element.Codec.ColorType != ColorType || element.Codec.Name != ActiveCodec.Name)
                    throw new ArgumentException($"{nameof(SetElement)} parameter '{nameof(element)}' cannot be assigned to SequentialArranger '{Name}'");

            ElementGrid[posX, posY] = element;
        }

        /// <summary>
        /// Changes each Element's codec and resizes the ElementPixelSize accordingly
        /// </summary>
        /// <param name="codec">New codec</param>
        public void ChangeCodec(IGraphicsCodec codec)
        {
            FileBitAddress address = GetInitialSequentialFileAddress();
            ElementPixelSize = new Size(codec.Width, codec.Height);

            ActiveCodec = codec;

            if (codec.Layout == ImageLayout.Single)
                Layout = ArrangerLayout.Single;
            else if (codec.Layout == ImageLayout.Tiled)
                Layout = ArrangerLayout.Tiled;

            ArrangerBitSize = ArrangerElementSize.Width * ArrangerElementSize.Height * codec.StorageSize;

            int elY = 0;

            for (int posY = 0; posY < ArrangerElementSize.Height; posY++)
            {
                int elX = 0;
                for (int posX = 0; posX < ArrangerElementSize.Width; posX++)
                {
                    var el = GetElement(posX, posY);
                    el = new ArrangerElement(elX, elY, el.DataFile, address, _codecs.CloneCodec(ActiveCodec), el.Palette);
                    SetElement(el, posX, posY);

                    if (ActiveCodec.Layout == ImageLayout.Tiled)
                        address += ActiveCodec.StorageSize;
                    else if (ActiveCodec.Layout == ImageLayout.Single)
                        address += (ElementPixelSize.Width + ActiveCodec.RowStride) * ActiveCodec.ColorDepth / 4; // TODO: Fix sequential arranger offsets to be bit-wise
                    else
                        throw new NotSupportedException();

                    elX += ElementPixelSize.Width;
                }
                elY += ElementPixelSize.Height;
            }

            address = GetInitialSequentialFileAddress();
            this.Move(address);
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

            var arranger = new ScatteredArranger(Name, Layout, elemsWidth, elemsHeight, ElementPixelSize.Width, ElementPixelSize.Height);

            for (int y = 0; y < elemsHeight; y++)
            {
                for (int x = 0; x < elemsWidth; x++)
                {
                    var elX = x * ElementPixelSize.Width;
                    var elY = y * ElementPixelSize.Height;
                    var codec = _codecs.CloneCodec(ElementGrid[x + elemX, y + elemY].Codec);

                    var el = GetElement(x + elemX, y + elemY).WithCodec(codec, elX, elY);
                    arranger.SetElement(el, x, y);
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
                    $"is in invalid {nameof(ArrangerMode)} ({Mode.ToString()})");

            return ElementGrid[0, 0].FileAddress;
        }

        /// <summary>
        /// Gets the GraphicsFormat name for a Sequential Arranger
        /// </summary>
        /// <returns></returns>
        public string GetSequentialGraphicsFormat() => ActiveCodec.Name;

        public override IEnumerable<IProjectResource> LinkedResources()
        {
            var set = new HashSet<IProjectResource>();

            foreach (var el in EnumerateElements())
            {
                set.Add(el.Palette);
                set.Add(el.DataFile);
            }

            return set;
        }
    }
}
