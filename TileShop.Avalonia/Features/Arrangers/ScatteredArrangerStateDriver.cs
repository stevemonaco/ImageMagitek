using System;
using ImageMagitek;
using TileShop.AvaloniaUI.ViewModels;

namespace TileShop.Shared.Input;

public class ScatteredArrangerStateDriver : ArrangerStateDriver<ScatteredArrangerEditorViewModel>
{
    public ScatteredArrangerStateDriver(ScatteredArrangerEditorViewModel viewModel) : base(viewModel)
    {
    }

    public override void MouseDown(double x, double y, MouseState mouseState)
    {
        var arranger = ViewModel.WorkingArranger;
        var tool = ViewModel.ActiveTool;

        int xc = Math.Clamp((int)x, 0, arranger.ArrangerPixelSize.Width - 1);
        int yc = Math.Clamp((int)y, 0, arranger.ArrangerPixelSize.Height - 1);
        var elementX = xc / arranger.ElementPixelSize.Width;
        var elementY = yc / arranger.ElementPixelSize.Height;

        if (tool == ScatteredArrangerTool.ApplyPalette && mouseState.LeftButtonPressed)
        {
            //_applyPaletteHistory = new ApplyPaletteHistoryAction(SelectedPalette.Palette);
            //TryApplyPalette(xc, yc, SelectedPalette.Palette);
        }
        else if (tool == ScatteredArrangerTool.PickPalette && mouseState.LeftButtonPressed)
        {
            //TryPickPalette(xc, yc);
        }
        else if (tool == ScatteredArrangerTool.RotateLeft && mouseState.LeftButtonPressed)
        {
            //var result = WorkingArranger.TryRotateElement(elementX, elementY, RotationOperation.Left);
            //if (result.HasSucceeded)
            //{
            //    AddHistoryAction(new RotateElementHistoryAction(elementX, elementY, RotationOperation.Left));
            //    IsModified = true;
            //    Render();
            //}
        }
        else if (tool == ScatteredArrangerTool.RotateRight && mouseState.LeftButtonPressed)
        {
            //var result = WorkingArranger.TryRotateElement(elementX, elementY, RotationOperation.Right);
            //if (result.HasSucceeded)
            //{
            //    AddHistoryAction(new RotateElementHistoryAction(elementX, elementY, RotationOperation.Right));
            //    IsModified = true;
            //    Render();
            //}
        }
        else if (tool == ScatteredArrangerTool.MirrorHorizontal && mouseState.LeftButtonPressed)
        {
            //var result = WorkingArranger.TryMirrorElement(elementX, elementY, MirrorOperation.Horizontal);
            //if (result.HasSucceeded)
            //{
            //    AddHistoryAction(new MirrorElementHistoryAction(elementX, elementY, MirrorOperation.Horizontal));
            //    IsModified = true;
            //    Render();
            //}
        }
        else if (tool == ScatteredArrangerTool.MirrorVertical && mouseState.LeftButtonPressed)
        {
            //var result = WorkingArranger.TryMirrorElement(elementX, elementY, MirrorOperation.Vertical);
            //if (result.HasSucceeded)
            //{
            //    AddHistoryAction(new MirrorElementHistoryAction(elementX, elementY, MirrorOperation.Vertical));
            //    IsModified = true;
            //    Render();
            //}
        }
        else if (tool == ScatteredArrangerTool.Select)
        {
            base.MouseDown(x, y, mouseState);
        }
    }
}
