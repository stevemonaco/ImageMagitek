using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Xaml.Interactions.DragAndDrop;
using ImageMagitek;
using TileShop.AvaloniaUI.Models;
using TileShop.AvaloniaUI.ViewModels;
using TileShop.Shared.Models;

namespace TileShop.AvaloniaUI.DragDrop;
public class ArrangerPasteDragHandler : IDragHandler
{
    public void AfterDragDrop(object? sender, PointerEventArgs e, object? context)
    {
        e.Handled = true;
    }

    public void BeforeDragDrop(object? sender, PointerEventArgs e, object? context)
    {
        if (context is not ArrangerEditorViewModel vm || sender is not IControl control)
        {
            return;
        }    

        if (vm.Selection.HasSelection)
        {
            var rect = vm.Selection.SelectionRect;
            var arranger = vm.WorkingArranger;

            ArrangerCopy copy = default;
            if (vm.SnapMode == SnapMode.Element)
            {
                int x = rect.SnappedLeft / arranger.ElementPixelSize.Width;
                int y = rect.SnappedTop / arranger.ElementPixelSize.Height;
                int width = rect.SnappedWidth / arranger.ElementPixelSize.Width;
                int height = rect.SnappedHeight / arranger.ElementPixelSize.Height;
                copy = arranger.CopyElements(x, y, width, height);
                (copy as ElementCopy).ProjectResource = vm.OriginatingProjectResource;
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

            var pos = e.GetPosition(control);

            vm.Paste = new ArrangerPaste(copy, vm.SnapMode)
            {
                DeltaX = (int)pos.X,
                DeltaY = (int)pos.Y
            };
            //dragInfo.Data = paste;
            //dragInfo.Effects = DragDropEffects.Copy | DragDropEffects.Move;

            vm.Selection = new ArrangerSelection(arranger, vm.SnapMode);
            e.Handled = true;
        }
        else if (vm.Paste is not null)
        {
            var pos = e.GetPosition(control);
            vm.Paste.DeltaX = (int)pos.X;
            vm.Paste.DeltaY = (int)pos.Y;
            vm.Paste.SnapMode = vm.SnapMode;

            e.Handled = true;
            //dragInfo.Data = Paste;
            //dragInfo.Effects = DragDropEffects.Copy | DragDropEffects.Move;
        }
    }
}
