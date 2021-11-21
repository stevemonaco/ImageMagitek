namespace TileShop.WPF.Behaviors;

public struct MouseCaptureArgs
{
    public double X { get; set; }
    public double Y { get; set; }
    public bool LeftButton { get; set; }
    public bool RightButton { get; set; }
    public MouseWheelDirection WheelDirection { get; set; }
}
