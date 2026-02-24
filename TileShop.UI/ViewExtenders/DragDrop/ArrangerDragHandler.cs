using Avalonia.Controls;
using Avalonia.Input;
using ImageMagitek;
using TileShop.Shared.Models;
using TileShop.UI.Controls;
using TileShop.UI.Models;
using TileShop.UI.ViewModels;

namespace TileShop.UI.DragDrop;
public class ArrangerDragHandler : IDragHandlerEx
{
    public object? Payload { get; set; }

    public void AfterDragDrop(object? sender, PointerEventArgs e, object? context)
    {
        e.Handled = true;
    }

    public void BeforeDragDrop(object? sender, PointerEventArgs e, object? context)
    {
        if (context is not GraphicsEditorViewModel vm || sender is not Control control)
        {
            return;
        }

        if (vm.Selection?.HasSelection is true)
        {
            var rect = vm.Selection.SelectionRect;
            var arranger = vm.WorkingArranger;

            ArrangerCopy copy;
            if (vm.SnapMode == SnapMode.Element)
            {
                int x = rect.SnappedLeft / arranger.ElementPixelSize.Width;
                int y = rect.SnappedTop / arranger.ElementPixelSize.Height;
                int width = rect.SnappedWidth / arranger.ElementPixelSize.Width;
                int height = rect.SnappedHeight / arranger.ElementPixelSize.Height;
                copy = arranger.CopyElements(x, y, width, height);
                ((ElementCopy)copy).ProjectResource = vm.OriginatingProjectResource;
            }
            else if (vm.SnapMode == SnapMode.Pixel && arranger.ColorType == PixelColorType.Indexed)
            {
                copy = arranger.CopyPixelsIndexed(rect.SnappedLeft, rect.SnappedTop, rect.SnappedWidth, rect.SnappedHeight);
            }
            else if (vm.SnapMode == SnapMode.Pixel && arranger.ColorType == PixelColorType.Direct)
            {
                copy = arranger.CopyPixelsDirect(rect.SnappedLeft, rect.SnappedTop, rect.SnappedWidth, rect.SnappedHeight);
            }
            else
            {
                return;
            }

            var screenPos = e.GetPosition(control);
            var pos = control is InfiniteCanvas canvas
                ? canvas.ScreenToLocalPoint(screenPos)
                : screenPos;

            var payload = new ArrangerPaste(copy, vm.SnapMode)
            {
                IsDragging = true
            };

            payload.Rect.MoveTo(rect.SnappedLeft, rect.SnappedTop);

            if (copy is not ElementCopy { Height: 1, Width: 1 })
            {
                payload.DeltaX = (int)pos.X - rect.SnappedLeft;
                payload.DeltaY = (int)pos.Y - rect.SnappedTop;
            };

            Payload = payload;
            vm.Paste = payload;

            vm.Selection = new ArrangerSelection(arranger, vm.SnapMode);
            e.Handled = true;
        }
        else if (vm.Paste is not null)
        {
            vm.Paste.IsDragging = true;
            var screenPos2 = e.GetPosition(control);
            var pos2 = control is InfiniteCanvas canvas2
                ? canvas2.ScreenToLocalPoint(screenPos2)
                : screenPos2;

            vm.Paste.DeltaX = (int)pos2.X - vm.Paste.Rect.SnappedLeft;
            vm.Paste.DeltaY = (int)pos2.Y - vm.Paste.Rect.SnappedTop;
        }
    }
}
