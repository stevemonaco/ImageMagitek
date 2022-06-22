using System;
using TileShop.AvaloniaUI.ViewModels;

namespace TileShop.Shared.Input;

//public abstract class PixelEditorStateDriver<TViewModel, TColor> : ArrangerStateDriver<TViewModel>
//    where TViewModel : PixelEditorViewModel<TColor>
//    where TColor : struct
//{
//    protected TColor _drawColor = default;

//    public PixelEditorStateDriver(TViewModel viewModel) : base(viewModel)
//    {
//    }

//    public override void MouseDown(double x, double y, MouseState mouseState)
//    {
//        if (ViewModel.ActiveTool == PixelTool.ColorPicker && mouseState.LeftButtonPressed)
//        {
//            ViewModel.PickColor((int)x, (int)y, ColorPriority.Primary);
//        }
//        else if (ViewModel.ActiveTool == PixelTool.ColorPicker && mouseState.RightButtonPressed)
//        {
//            ViewModel.PickColor((int)x, (int)y, ColorPriority.Secondary);
//        }
//        else if (ViewModel.ActiveTool == PixelTool.Pencil && mouseState.LeftButtonPressed)
//        {
//            _drawColor = ViewModel.PrimaryColor;
//            ViewModel.StartDraw((int)x, (int)y, ColorPriority.Primary);
//            ViewModel.SetPixel((int)x, (int)y, _drawColor);
//        }
//        else if (ViewModel.ActiveTool == PixelTool.Pencil && mouseState.RightButtonPressed)
//        {
//            _drawColor = ViewModel.SecondaryColor;
//            ViewModel.StartDraw((int)x, (int)y, ColorPriority.Secondary);
//            ViewModel.SetPixel((int)x, (int)y, _drawColor);
//        }
//        else if (ViewModel.ActiveTool == PixelTool.FloodFill && mouseState.LeftButtonPressed)
//        {
//            ViewModel.FloodFill((int)x, (int)y, ViewModel.PrimaryColor);
//        }
//        else if (ViewModel.ActiveTool == PixelTool.FloodFill && mouseState.RightButtonPressed)
//        {
//            ViewModel.FloodFill((int)x, (int)y, ViewModel.SecondaryColor);
//        }
//        else
//        {
//            base.MouseDown(x, y, mouseState);
//        }
//    }

//    public override void MouseUp(double x, double y, MouseState mouseState)
//    {
//        if (ViewModel.IsDrawing && !mouseState.LeftButtonPressed && !mouseState.RightButtonPressed)
//        {
//            ViewModel.StopDrawing();
//        }

//        base.MouseUp(x, y, mouseState);
//    }

//    public override void MouseMove(double x, double y)
//    {
//        var bounds = ViewModel.WorkingArranger.ArrangerPixelSize;
//        int xc = Math.Clamp((int)x, 0, bounds.Width - 1);
//        int yc = Math.Clamp((int)y, 0, bounds.Height - 1);

//        if (x < 0 || x >= bounds.Width || y < 0 || y >= bounds.Height)
//            return;

//        if (ViewModel.IsDrawing && ViewModel.ActiveTool == PixelTool.Pencil)
//            ViewModel.SetPixel((int)x, (int)y, _drawColor);
//        else if (ViewModel.IsDrawing && ViewModel.ActiveTool == PixelTool.Pencil)
//            ViewModel.SetPixel((int)x, (int)y, _drawColor);
//        else
//            base.MouseMove(x, y);
//    }

//    public override void MouseLeave()
//    {
//        if (ViewModel.ActiveTool == PixelTool.Pencil && ViewModel.IsDrawing)
//        {
//            ViewModel.StopDrawing();
//        }
//        base.MouseLeave();
//    }
//}
