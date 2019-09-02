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
        private string _codecName;
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
            _codecName = codecName;

            ActiveCodec = _codecs.GetCodec(_codecName);

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

            var codec = _codecs.GetCodec(_codecName);
            ElementPixelSize = new Size(codec.Width, codec.Height);

            if (ElementGrid is null) // New Arranger being resized
                address = 0;
            else
                address = GetInitialSequentialFileAddress();

            ElementGrid = new ArrangerElement[arrangerWidth, arrangerHeight];

            ArrangerElementSize = new Size(arrangerWidth, arrangerHeight);
            ArrangerBitSize = arrangerWidth * arrangerHeight * codec.StorageSize;

            int x = 0;
            int y = 0;

            for (int i = 0; i < arrangerHeight; i++)
            {
                x = 0;
                for (int j = 0; j < arrangerWidth; j++)
                {
                    ArrangerElement el = new ArrangerElement()
                    {
                        Parent = this,
                        FileAddress = address,
                        X1 = x,
                        Y1 = y,
                        Width = ElementPixelSize.Width,
                        Height = ElementPixelSize.Height,
                        DataFile = dataFile
                    };

                    el.Codec = _codecs.GetCodec(_codecName, el.Width, el.Height);

                    ElementGrid[j, i] = el;

                    if (el.Codec.Layout == ImageLayout.Tiled)
                        address += el.Codec.StorageSize;
                    else // Linear
                        address += (ElementPixelSize.Width + el.Codec.RowStride) * el.Codec.ColorDepth / 4; // TODO: Fix sequential arranger offsets to be bit-wise

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

            int x = 0;
            int y = 0;

            for (int i = 0; i < ArrangerElementSize.Height; i++)
            {
                x = 0;
                for (int j = 0; j < ArrangerElementSize.Width; j++)
                {
                    ElementGrid[j, i].FileAddress = address;
                    ElementGrid[j, i].X1 = x;
                    ElementGrid[j, i].Y1 = y;
                    ElementGrid[j, i].Width = ElementPixelSize.Width;
                    ElementGrid[j, i].Height = ElementPixelSize.Height;
                    ElementGrid[j, i].Codec = _codecs.CloneCodec(codec);

                    if (codec.Layout == ImageLayout.Tiled)
                        address += codec.StorageSize;
                    else // Linear
                        address += (ElementPixelSize.Width + codec.RowStride) * codec.ColorDepth / 4; // TODO: Fix sequential arranger offsets to be bit-wise

                    x += ElementPixelSize.Width;
                }
                y += ElementPixelSize.Height;
            }

            address = GetInitialSequentialFileAddress();
            address = this.Move(address);
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
        public string GetSequentialGraphicsFormat() => _codecName;

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
