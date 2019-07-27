using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;

namespace ImageMagitek.Colors
{
    public interface IPalette
    {

        bool IsViewOnly { get; }

        /// <summary>
        /// DataFile which contains the palette
        /// </summary>
        DataFile DataFile { get; set; }

        /// <summary>
        /// Address of the palette within the file
        /// </summary>
        FileBitAddress FileAddress { get; set; }

        /// <summary>
        /// Number of color entries in the palette
        /// </summary>
        int Entries { get; set; }

        /// <summary>
        /// Specifies if the Palette has an alpha channel
        /// </summary>
        bool HasAlpha { get; set; }

        /// <summary>
        /// Specifies if the palette's 0-index is automatically treated as transparent
        /// </summary>
        bool ZeroIndexTransparent { get; set; }

        /// <summary>
        /// Specifies the palette's storage source
        /// </summary>
        PaletteStorageSource StorageSource { get; set; }

        NativeColor GetNativeColor(int index);
        ForeignColor GetForeignColor(int index);
        Color GetColor(int index);
        byte GetIndexByNativeColor(NativeColor color, bool exactColorOnly);
        void SetPaletteForeignColor(int index, ForeignColor foreignColor);
        void SetPaletteForeignColor(int index, byte A, byte R, byte G, byte B);

        NativeColor this[int index] { get; }
    }
}
