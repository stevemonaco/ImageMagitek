using Caliburn.Micro;
using ImageMagitek;
using System;
using System.Collections.Generic;
using System.Text;
using TileShop.WPF.Behaviors;
using TileShop.WPF.Helpers;

namespace TileShop.WPF.ViewModels
{
    public class SequentialArrangerEditorViewModel : ArrangerEditorViewModel, IMouseCaptureProxy
    {
        //public override event EventHandler Capture;
        //public override event EventHandler Release;

        public SequentialArrangerEditorViewModel(SequentialArranger arranger, IEventAggregator events)
        {
            Resource = arranger;
            _arranger = arranger;
            _events = events;

            MoveHome();
        }

        public void MoveByteDown() => Move(ArrangerMoveType.ByteDown);
        public void MoveByteUp() => Move(ArrangerMoveType.ByteUp);
        public void MoveRowDown() => Move(ArrangerMoveType.RowDown);
        public void MoveRowUp() => Move(ArrangerMoveType.RowUp);
        public void MoveColumnRight() => Move(ArrangerMoveType.ColRight);
        public void MoveColumnLeft() => Move(ArrangerMoveType.ColLeft);
        public void MovePageDown() => Move(ArrangerMoveType.PageDown);
        public void MovePageUp() => Move(ArrangerMoveType.PageUp);
        public void MoveHome() => Move(ArrangerMoveType.Home);
        public void MoveEnd() => Move(ArrangerMoveType.End);
        public void ExpandColumn() => ResizeArranger(_arranger.ArrangerElementSize.Width + 1, _arranger.ArrangerElementSize.Height);
        public void ExpandRow() => ResizeArranger(_arranger.ArrangerElementSize.Width, _arranger.ArrangerElementSize.Height + 1);
        public void ShrinkColumn() => ResizeArranger(_arranger.ArrangerElementSize.Width - 1, _arranger.ArrangerElementSize.Height);
        public void ShrinkRow() => ResizeArranger(_arranger.ArrangerElementSize.Width, _arranger.ArrangerElementSize.Height - 1);


        private void Move(ArrangerMoveType moveType)
        {
            (_arranger as SequentialArranger).Move(moveType);
            _arrangerImage.Invalidate();
            _arrangerImage.Render(_arranger);
            ArrangerSource = new ImageRgba32Source(_arrangerImage.Image);
        }

        private void ResizeArranger(int arrangerWidth, int arrangerHeight)
        {
            if (arrangerWidth <= 0 || arrangerHeight <= 0)
                return;

            (_arranger as SequentialArranger).Resize(arrangerWidth, arrangerHeight);
            _arrangerImage.Invalidate();
            _arrangerImage.Render(_arranger);
            ArrangerSource = new ImageRgba32Source(_arrangerImage.Image);
        }

        public override void OnMouseDown(object sender, MouseCaptureArgs e)
        {
            //throw new NotImplementedException();
        }

        public override void OnMouseLeave(object sender, MouseCaptureArgs e)
        {
            //throw new NotImplementedException();
        }

        public override void OnMouseMove(object sender, MouseCaptureArgs e)
        {
            //throw new NotImplementedException();
        }

        public override void OnMouseUp(object sender, MouseCaptureArgs e)
        {
            //throw new NotImplementedException();
        }
    }
}
