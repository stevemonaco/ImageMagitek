using System;
using System.Collections.Generic;
using System.Drawing;
using ImageMagitek.Project;
using ImageMagitek.Codec;

namespace ImageMagitek
{
    public class SequentialArranger : Arranger
    {
        /// <summary>
        /// Gets the filesize of the file associated with a Sequential Arranger
        /// </summary>
        public long FileSize { get; private set; }

        /// <summary>
        /// Gets the filesize of the file associated with a Sequential Arranger
        /// </summary>
        public long FileAddress { get; private set; }

        /// <summary>
        /// Number of bits required to be read from file sequentially
        /// </summary>
        public long ArrangerBitSize { get; private set; }

        private ICodecFactory _codecs;
        private string _codecName;

        public SequentialArranger()
        {
            Mode = ArrangerMode.SequentialArranger;
        }

        public SequentialArranger(int arrangerWidth, int arrangerHeight, DataFile dataFile, ICodecFactory codecFactory, string codecName)
        {
            Mode = ArrangerMode.SequentialArranger;
            FileSize = dataFile.Stream.Length;
            Name = dataFile.Name;
            _codecs = codecFactory;
            _codecName = codecName;

            Resize(arrangerWidth, arrangerHeight, dataFile, codecName);
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

            Resize(arrangerWidth, arrangerHeight, ElementGrid[0, 0].DataFile, _codecName);
        }

        /// <summary>
        /// Resizes a Sequential Arranger with a new number of elements
        /// </summary>
        /// <param name="arrangerWidth">Width of Arranger in Elements</param>
        /// <param name="arrangerHeight">Height of Arranger in Elements</param>
        /// <param name="dataFileKey">DataFile key in IResourceManager</param>
        /// <param name="codecName">Codec name for encoding/decoding Elements</param>
        /// <returns></returns>
        private FileBitAddress Resize(int arrangerWidth, int arrangerHeight, DataFile dataFile, string codecName)
        {
            if (Mode != ArrangerMode.SequentialArranger)
                throw new InvalidOperationException($"{nameof(Resize)} property '{nameof(Mode)}' is in invalid {nameof(ArrangerMode)} ({Mode.ToString()})");

            _codecName = codecName;
            FileBitAddress address;

            if (ElementGrid is null) // New Arranger being resized
                address = 0;
            else
                address = GetInitialSequentialFileAddress();

            ElementGrid = new ArrangerElement[arrangerWidth, arrangerHeight];

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
                        DataFile = dataFile,
                    };

                    el.Codec = _codecs.GetCodec(codecName, el.Width, el.Height);

                    ElementGrid[j, i] = el;

                    if (el.Codec.Layout == ImageLayout.Tiled)
                        address += el.Codec.StorageSize;
                    else // Linear
                        address += (ElementPixelSize.Width + el.Codec.RowStride) * el.Codec.ColorDepth / 4; // TODO: Fix sequential arranger offsets to be bit-wise

                    x += ElementPixelSize.Width;
                }
                y += ElementPixelSize.Height;
            }

            ArrangerElement lastElem = ElementGrid[arrangerWidth - 1, arrangerHeight - 1];
            ArrangerElementSize = new Size(arrangerWidth, arrangerHeight);
            ElementPixelSize = new Size(ElementPixelSize.Width, ElementPixelSize.Height);

            ArrangerBitSize = arrangerWidth * arrangerHeight * lastElem.Codec.StorageSize;

            address = GetInitialSequentialFileAddress();
            address = this.Move(address);

            return address;
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

        public override IEnumerable<ProjectResourceBase> LinkedResources()
        {
            var set = new HashSet<ProjectResourceBase>();

            foreach (var el in EnumerateElements())
            {
                set.Add(el.Palette);
                set.Add(el.DataFile);
            }

            foreach (var item in set)
                yield return item;
        }

        /// <summary>
        /// Sets the GraphicsFormat name and Element size for a Sequential Arranger
        /// </summary>
        /// <param name="FormatName">Name of the GraphicsFormat</param>
        /// <param name="ElementSize">Size of each Element in pixels</param>
        /// <returns></returns>
        /*public bool SetGraphicsFormat(string FormatName, Size ElementSize)
        {
            if (ElementGrid is null)
                throw new NullReferenceException($"{nameof(SetGraphicsFormat)} property '{nameof(ElementGrid)}' was null");

            if (Mode != ArrangerMode.SequentialArranger)
                throw new InvalidOperationException($"{nameof(SetGraphicsFormat)} property '{nameof(Mode)}' is in invalid {nameof(ArrangerMode)} ({Mode.ToString()})");

            FileBitAddress address = ElementGrid[0, 0].FileAddress;
            //GraphicsFormat format = ElementGrid[0, 0].GraphicsFormat;

            ElementPixelSize = ElementSize;

            int elembitsize = format.StorageSize;
            ArrangerBitSize = ArrangerElementSize.Width * ArrangerElementSize.Height * elembitsize;

            if (FileSize * 8 < address + ArrangerBitSize)
                address = new FileBitAddress(FileSize * 8 - ArrangerBitSize);

            for (int i = 0; i < ArrangerElementSize.Height; i++)
            {
                for (int j = 0; j < ArrangerElementSize.Width; j++)
                {
                    ElementGrid[j, i].FileAddress = address;
                    ElementGrid[j, i].FormatName = FormatName;
                    ElementGrid[j, i].Width = ElementPixelSize.Width;
                    ElementGrid[j, i].Height = ElementPixelSize.Height;
                    ElementGrid[j, i].X1 = j * ElementPixelSize.Width;
                    ElementGrid[j, i].Y1 = i * ElementPixelSize.Height;
                    address += elembitsize;
                }
            }

            ArrangerElement LastElem = ElementGrid[ArrangerElementSize.Width - 1, ArrangerElementSize.Height - 1];

            return true;
        }*/
    }
}
