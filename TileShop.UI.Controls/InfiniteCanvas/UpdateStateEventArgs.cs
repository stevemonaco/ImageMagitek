using System;

namespace TileShop.UI.Controls;
public sealed class UpdateStateEventArgs : EventArgs
{
    public TimeSpan TimeElapsed { get; }
    
    public UpdateStateEventArgs(TimeSpan timeElapsed)
    {
        TimeElapsed = timeElapsed;
    }
}