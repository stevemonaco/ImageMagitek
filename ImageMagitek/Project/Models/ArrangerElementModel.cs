using System;
using System.Collections.Generic;
using System.Text;

namespace ImageMagitek.Project.Models
{
    internal class ArrangerElementModel
    {
        public ArrangerElementModel(string dataFileKey, string paletteKey, string formatName, FileBitAddress fileAddress, int positionX, int positionY)
        {
            DataFileKey = dataFileKey;
            PaletteKey = paletteKey;
            FormatName = formatName;
            FileAddress = fileAddress;
            PositionX = positionX;
            PositionY = positionY;
        }

        public string DataFileKey { get; }

        public string PaletteKey { get; }

        public string FormatName { get; }

        public FileBitAddress FileAddress { get; }

        public int PositionX { get; }

        public int PositionY { get; }
    }
}
