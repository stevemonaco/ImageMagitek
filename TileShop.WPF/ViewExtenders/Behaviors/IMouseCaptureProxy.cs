using System;

namespace TileShop.WPF.Behaviors
{
    public interface IMouseCaptureProxy
    {
        event EventHandler Capture;
        event EventHandler Release;

        void OnMouseDown(object sender, MouseCaptureArgs e);
        void OnMouseLeave(object sender, MouseCaptureArgs e);
        void OnMouseMove(object sender, MouseCaptureArgs e);
        void OnMouseUp(object sender, MouseCaptureArgs e);
        void OnMouseWheel(object sender, MouseCaptureArgs e);
    }
}
