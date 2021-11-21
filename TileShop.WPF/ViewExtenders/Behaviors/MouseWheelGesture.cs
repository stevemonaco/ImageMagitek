using System.Windows.Input;

namespace TileShop.WPF.Behaviors;

public partial class MouseWheelGesture : MouseGesture
{
    public static MouseWheelGesture CtrlScrollDown
        => new MouseWheelGesture(ModifierKeys.Control) { Direction = MouseWheelDirection.Down };

    public static MouseWheelGesture CtrlScrollUp
    => new MouseWheelGesture(ModifierKeys.Control) { Direction = MouseWheelDirection.Up };

    public MouseWheelGesture() : base(MouseAction.WheelClick)
    {
    }

    public MouseWheelGesture(ModifierKeys modifiers) : base(MouseAction.WheelClick, modifiers)
    {
    }

    public MouseWheelDirection Direction { get; set; }

    public override bool Matches(object targetElement, InputEventArgs inputEventArgs)
    {
        if (!base.Matches(targetElement, inputEventArgs)) return false;
        if (!(inputEventArgs is MouseWheelEventArgs args)) return false;
        switch (Direction)
        {
            case MouseWheelDirection.None:
                return args.Delta == 0;
            case MouseWheelDirection.Up:
                return args.Delta > 0;
            case MouseWheelDirection.Down:
                return args.Delta < 0;
            default:
                return false;
        }
    }
}
