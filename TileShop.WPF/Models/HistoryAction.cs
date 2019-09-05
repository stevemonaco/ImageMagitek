using Caliburn.Micro;
using System;
using System.Collections.Generic;
using System.Text;

namespace TileShop.WPF.Models
{
    public abstract class HistoryAction : PropertyChangedBase
    {
        public abstract string Name { get; }
    }
}
