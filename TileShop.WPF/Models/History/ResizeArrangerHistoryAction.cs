namespace TileShop.WPF.Models;

public class ResizeArrangerHistoryAction : HistoryAction
{
    public override string Name => "Resize Arranger";

    public int Width { get; }
    public int Height { get; }

    public ResizeArrangerHistoryAction(int width, int height)
    {
        Width = width;
        Height = height;
    }
}
