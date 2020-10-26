using System;
using System.Collections.Generic;
using System.Text;

namespace ImageMagitek.Colors
{
    public interface ITableColor : IColor
    {
        public int ColorMax { get; }
    }
}
