using System;
using System.Collections.Generic;
using System.Text;

namespace ImageMagitek.Colors
{
    public interface IColorConverter<TForeign, TNative>
        where TForeign : IColor
        where TNative : IColor
    {
        TForeign ToForeignColor(TNative nc);
        TNative ToNativeColor(TForeign fc);
    }
}
