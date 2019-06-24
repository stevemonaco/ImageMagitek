using System;
using System.Collections.Generic;
using System.Text;

namespace ImageMagitek.Project.Models
{
    internal class PaletteModel
    {
        public ColorModel ColorModel { get; }

        public string DataFileKey { get; }

        public FileBitAddress FileAddress { get; }

        public int Entries { get; }

        public bool HasAlpha { get; }

        public bool ZeroIndexTransparent { get; }
    }
}
