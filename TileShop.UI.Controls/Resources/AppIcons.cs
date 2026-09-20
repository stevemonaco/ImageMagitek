using Avalonia;
using Avalonia.Media;

namespace TileShop.UI.Controls;

public static partial class AppIcons
{
    public static DrawingImage NewNodeFolder { get; } = CreateNewNodeFolder();
    public static DrawingImage NewNodeFolderOpen { get; } = CreateNewNodeFolderOpen();
    public static DrawingImage NewNodePalette { get; } = CreateNewNodePalette();
    public static DrawingImage NewNodeArranger { get; } = CreateNewNodeArranger();
    public static DrawingImage NewNodeFile { get; } = CreateNewNodeFile();
    public static DrawingImage NewNodeProject { get; } = CreateNewNodeProject();

    private static DrawingImage CreateNewNodeFolder()
    {
        var group = new DrawingGroup();
        group.Children.Add(new GeometryDrawing
        {
            Geometry = StreamGeometry.Parse("M2 5C2 4.4 2.4 4 3 4H7L8.5 5.5H15C15.6 5.5 16 5.9 16 6.5V13C16 13.6 15.6 14 15 14H3C2.4 14 2 13.6 2 13V5Z"),
            Brush = new SolidColorBrush(Color.Parse("#33F59E0B")),
        });
        group.Children.Add(new GeometryDrawing
        {
            Geometry = StreamGeometry.Parse("M2 5C2 4.4 2.4 4 3 4H7L8.5 5.5H15C15.6 5.5 16 5.9 16 6.5V13C16 13.6 15.6 14 15 14H3C2.4 14 2 13.6 2 13V5Z"),
            Pen = new Pen(new SolidColorBrush(Color.Parse("#D97706")), 1.1) { LineJoin = PenLineJoin.Round },
        });
        return new DrawingImage(group);
    }

    private static DrawingImage CreateNewNodeFolderOpen()
    {
        var stroke = new SolidColorBrush(Color.Parse("#D97706"));
        var group = new DrawingGroup();
        group.Children.Add(new GeometryDrawing
        {
            Geometry = StreamGeometry.Parse("M2 5.5C2 4.4 2.9 3.5 4 3.5H7L8.5 5.5H14C15.1 5.5 16 6.4 16 7.5V7.5H4.5L2 13V5.5Z"),
            Brush = new SolidColorBrush(Color.Parse("#40F59E0B")),
        });
        group.Children.Add(new GeometryDrawing
        {
            Geometry = StreamGeometry.Parse("M2.5 13L4.5 7.5H16L13.5 13.5H4C3.2 13.5 2.3 13.3 2.5 13Z"),
            Brush = new SolidColorBrush(Color.Parse("#66F59E0B")),
        });
        group.Children.Add(new GeometryDrawing
        {
            Geometry = StreamGeometry.Parse("M2 5.5C2 4.4 2.9 3.5 4 3.5H7L8.5 5.5H14C15.1 5.5 16 6.4 16 7.5V7.5H4.5L2 13V5.5Z"),
            Pen = new Pen(stroke, 1) { LineJoin = PenLineJoin.Round },
        });
        group.Children.Add(new GeometryDrawing
        {
            Geometry = StreamGeometry.Parse("M2.5 13L4.5 7.5H16L13.5 13.5H4C3.2 13.5 2.3 13.3 2.5 13Z"),
            Pen = new Pen(stroke, 1) { LineJoin = PenLineJoin.Round },
        });
        return new DrawingImage(group);
    }

    private static DrawingImage CreateNewNodePalette()
    {
        var pink = Color.Parse("#EC4899");
        var group = new DrawingGroup();
        group.Children.Add(new GeometryDrawing
        {
            Geometry = new EllipseGeometry(new Rect(2, 2, 14, 14)),
            Brush = new SolidColorBrush(Color.FromArgb(31, pink.R, pink.G, pink.B)),
            Pen = new Pen(new SolidColorBrush(pink), 1.1),
        });
        group.Children.Add(new GeometryDrawing
        {
            Geometry = new EllipseGeometry(new Rect(5.7, 5.2, 2.6, 2.6)),
            Brush = new SolidColorBrush(Color.Parse("#EF4444")),
        });
        group.Children.Add(new GeometryDrawing
        {
            Geometry = new EllipseGeometry(new Rect(9.7, 5.2, 2.6, 2.6)),
            Brush = new SolidColorBrush(Color.Parse("#3B82F6")),
        });
        group.Children.Add(new GeometryDrawing
        {
            Geometry = new EllipseGeometry(new Rect(5.7, 9.7, 2.6, 2.6)),
            Brush = new SolidColorBrush(Color.Parse("#22C55E")),
        });
        group.Children.Add(new GeometryDrawing
        {
            Geometry = new EllipseGeometry(new Rect(9.7, 9.7, 2.6, 2.6)),
            Brush = new SolidColorBrush(Color.Parse("#F59E0B")),
        });
        return new DrawingImage(group);
    }

    private static DrawingImage CreateNewNodeArranger()
    {
        var cyan = Color.Parse("#06B6D4");
        var cyanBrush = new SolidColorBrush(cyan);
        var group = new DrawingGroup();
        group.Children.Add(new GeometryDrawing
        {
            Geometry = StreamGeometry.Parse("M4 3H14A2 2 0 0 1 16 5V13A2 2 0 0 1 14 15H4A2 2 0 0 1 2 13V5A2 2 0 0 1 4 3Z"),
            Brush = new SolidColorBrush(Color.FromArgb(38, cyan.R, cyan.G, cyan.B)),
            Pen = new Pen(cyanBrush, 1.1),
        });
        group.Children.Add(new GeometryDrawing
        {
            Geometry = new RectangleGeometry(new Rect(4.5, 5.5, 4, 3)),
            Brush = new SolidColorBrush(Color.FromArgb(128, cyan.R, cyan.G, cyan.B)),
        });
        group.Children.Add(new GeometryDrawing
        {
            Geometry = new RectangleGeometry(new Rect(4.5, 10, 9, 1.2)),
            Brush = new SolidColorBrush(Color.FromArgb(89, cyan.R, cyan.G, cyan.B)),
        });
        group.Children.Add(new GeometryDrawing
        {
            Geometry = new RectangleGeometry(new Rect(10, 5.5, 3.5, 3)),
            Brush = new SolidColorBrush(Color.FromArgb(77, cyan.R, cyan.G, cyan.B)),
        });
        return new DrawingImage(group);
    }

    private static DrawingImage CreateNewNodeFile()
    {
        var purple = Color.Parse("#8B5CF6");
        var purpleBrush = new SolidColorBrush(purple);
        var group = new DrawingGroup();
        group.Children.Add(new GeometryDrawing
        {
            Geometry = StreamGeometry.Parse("M5 2H11L14 5V15C14 15.6 13.6 16 13 16H5C4.4 16 4 15.6 4 15V3C4 2.4 4.4 2 5 2Z"),
            Brush = new SolidColorBrush(Color.FromArgb(31, purple.R, purple.G, purple.B)),
        });
        group.Children.Add(new GeometryDrawing
        {
            Geometry = StreamGeometry.Parse("M5 2H11L14 5V15C14 15.6 13.6 16 13 16H5C4.4 16 4 15.6 4 15V3C4 2.4 4.4 2 5 2Z"),
            Pen = new Pen(purpleBrush, 1.1) { LineJoin = PenLineJoin.Round },
        });
        group.Children.Add(new GeometryDrawing
        {
            Geometry = StreamGeometry.Parse("M11 2V5H14"),
            Pen = new Pen(purpleBrush, 1.1) { LineJoin = PenLineJoin.Round },
        });
        group.Children.Add(new GeometryDrawing
        {
            Geometry = StreamGeometry.Parse("M7 9H12M7 11.5H11"),
            Pen = new Pen(purpleBrush, 0.9) { LineCap = PenLineCap.Round },
        });
        return new DrawingImage(group);
    }

    private static DrawingImage CreateNewNodeProject()
    {
        var indigo = Color.Parse("#6366F1");
        var indigoBrush = new SolidColorBrush(indigo);
        var group = new DrawingGroup();
        group.Children.Add(new GeometryDrawing
        {
            Geometry = StreamGeometry.Parse("M5 2H13A3 3 0 0 1 16 5V13A3 3 0 0 1 13 16H5A3 3 0 0 1 2 13V5A3 3 0 0 1 5 2Z"),
            Brush = new SolidColorBrush(Color.FromArgb(38, indigo.R, indigo.G, indigo.B)),
        });
        group.Children.Add(new GeometryDrawing
        {
            Geometry = StreamGeometry.Parse("M5 2H13A3 3 0 0 1 16 5V13A3 3 0 0 1 13 16H5A3 3 0 0 1 2 13V5A3 3 0 0 1 5 2Z"),
            Pen = new Pen(indigoBrush, 1.2),
        });
        group.Children.Add(new GeometryDrawing
        {
            Geometry = StreamGeometry.Parse("M6 6H12M6 9H12M6 12H10"),
            Pen = new Pen(indigoBrush, 1.2) { LineCap = PenLineCap.Round },
        });
        return new DrawingImage(group);
    }
}
