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
        /// Gets the filesize of the file associated with a Sequential Arranger
        /// </summary>
        public long FileSize { get; private set; }

        /// <summary>
        /// Gets the current file address of the file associated with a Sequential Arranger
        /// </summary>
        public long FileAddress { get; private set; }

        /// <summary>
        /// Number of bits required to be read from file sequentially
        /// </summary>
        public long ArrangerBitSize { get; private set; }
        public override bool ShouldBeSerialized { get; set; } = true;

        public IGraphicsCodec ActiveCodec { get; private set; }

        private ICodecFactory _codecs;
        private DataFile _dataFile;

        public SequentialArranger()
        {
            Mode = ArrangerMode.SequentialArranger;
        }

        public SequentialArranger(int arrangerWidth, int arrangerHeight, DataFile dataFile, ICodecFactory codecFactory, string codecName)
        {
            Mode = ArrangerMode.SequentialArranger;
            FileSize = dataFile.Stream.Length;
            Name = dataFile.Name;
            _dataFile = dataFile;
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
            if (Mode != ArrangerMode.SequentialArranger)
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
            if (Mode != ArrangerMode.SequentialArranger)
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

            int y = 0;

            for (int i = 0; i < arrangerHeight; i++)
            {
                int x = 0;
                for (int j = 0; j < arrangerWidth; j++)
                {
                    ArrangerElement el = ElementGrid[j, i] ??
                        new ArrangerElement() { Codec = _codecs.CloneCodec(ActiveCodec) };

                    el.FileAddress = address;
                    el.X1 = x;
                    el.Y1 = y;
                    el.Width = ElementPixelSize.Width;
                    el.Height = ElementPixelSize.Height;
                    el.DataFile = dataFile;

                    ElementGrid[j, i] = el;

                    if (el.Codec.Layout == ImageLayout.Tiled)
                        address += ActiveCodec.StorageSize;
                    else // Linear
                        address += (ElementPixelSize.Width + ActiveCodec.RowStride) * ActiveCodec.ColorDepth / 4; // TODO: Fix sequential arranger offsets to be bit-wise

                    x += ElementPixelSize.Width;
                }
                y += ElementPixelSize.Height;
            }

            address = GetInitialSequentialFileAddress();
            address = this.Move(address);

            return address;
        }

        public void ChangeCodec(IGraphicsCodec codec)
        {
            FileBitAddress address = GetInitialSequentialFileAddress();
            ElementPixelSize = new Size(codec.Width, codec.Height);

            ActiveCodec = codec;

            if (codec.Layout == ImageLayout.Linear)
                Layout = ArrangerLayout.LinearArranger;
            else if (codec.Layout == ImageLayout.Tiled)
                Layout = ArrangerLayout.TiledArranger;

            ArrangerBitSize = ArrangerElementSize.Width * ArrangerElementSize.Height * codec.StorageSize;

            int y = 0;

            for (int i = 0; i < ArrangerElementSize.Height; i++)
            {
                int x = 0;
                for (int j = 0; j < ArrangerElementSize.Width; j++)
                {
                    ElementGrid[j, i].FileAddress = address;
                    ElementGrid[j, i].X1 = x;
                    ElementGrid[j, i].Y1 = y;
                    ElementGrid[j, i].Width = ElementPixelSize.Width;
                    ElementGrid[j, i].Height = ElementPixelSize.Height;
                    ElementGrid[j, i].Codec = _codecs.CloneCodec(ActiveCodec);

                    if (ActiveCodec.Layout == ImageLayout.Tiled)
                        address += ActiveCodec.StorageSize;
                    else // Linear
                        address += (ElementPixelSize.Width + ActiveCodec.RowStride) * ActiveCodec.ColorDepth / 4; // TODO: Fix sequential arranger offsets to be bit-wise

                    x += ElementPixelSize.Width;
                }
                y += ElementPixelSize.Height;
            }

            address = GetInitialSequentialFileAddress();
            this.Move(address);
        }

        public override Arranger CloneArranger()
        {
            if (Layout == ArrangerLayout.TiledArranger || Layout == ArrangerLayout.LinearArranger)
                return CloneArranger(0, 0, ArrangerPixelSize.Width, ArrangerPixelSize.Height);
            else
                throw new NotSupportedException($"{nameof(CloneArranger)} with {nameof(ArrangerLayout)} '{Layout}' is not supported");
        }

        /// <summary>
        /// Clones a subsection of the Arranger
        /// </summary>
        /// <param name="posX">Left edge of Arranger in pixel coordinates</param>
        /// <param name="posY">Top edge of Arranger in pixel coordinates</param>
        /// <param name="width">Width of Arranger in pixels</param>
        /// <param name="height">Height of Arranger in pixels</param>
        /// <returns></returns>
        public override Arranger CloneArranger(int posX, int posY, int width, int height)
        {
            if (posX < 0 || posX + width > ArrangerPixelSize.Width || posY < 0 || posY + height > ArrangerPixelSize.Height)
                throw new ArgumentOutOfRangeException($"{nameof(CloneArranger)} parameters ({nameof(posX)}: {posX}, {nameof(posY)}: {posY}, {nameof(width)}: {width}, {nameof(height)}: {height})" +
                    $" were outside of the bounds of arranger '{Name}' of size (width: {ArrangerPixelSize.Width}, height: {ArrangerPixelSize.Height})");

            if (Layout == ArrangerLayout.LinearArranger)
            {
                if (posX != 0 || posY != 0 || width != ArrangerPixelSize.Width || height != ArrangerPixelSize.Height)
                    throw new InvalidOperationException($"{nameof(CloneArranger)} of a LinearArranger must have the same dimensions as the original");
                return CloneArrangerInternal(posX, posY, width, height);
            }

            return CloneArrangerInternal(posX, posY, width, height);
        }

        private Arranger CloneArrangerInternal(int posX, int posY, int width, int height)
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
                    var el = GetElement(x + elemX, y + elemY).Clone();
                    el.X1 = x * ElementPixelSize.Width;
                    el.Y1 = y * ElementPixelSize.Height;
                    el.Codec = _codecs.CloneCodec(ElementGrid[x + elemX, y + elemY].Codec);
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

            if (Mode != ArrangerMode.SequentialArranger)
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

            foreach (var item in set)
                yield return item;
        }
    }
}
