using Caliburn.Micro;
using ImageMagitek;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using TileShop.Shared.EventModels;
using TileShop.Shared.Services;
using TileShop.WPF.Behaviors;
using TileShop.WPF.Helpers;

namespace TileShop.WPF.ViewModels
{
    public class SequentialArrangerEditorViewModel : ArrangerEditorViewModel, IMouseCaptureProxy
    {
        private ICodecService _codecService;

        private BindableCollection<string> _codecNames = new BindableCollection<string>();
        public BindableCollection<string> CodecNames
        {
            get => _codecNames;
            set => Set(ref _codecNames, value);
        }

        private string _selectedCodecName;
        public string SelectedCodecName
        {
            get => _selectedCodecName;
            set
            {
                Set(ref _selectedCodecName, value);
                ChangeCodec();
            }
        }

        private int _tiledElementWidth = 8;
        public int TiledElementWidth
        {
            get => _tiledElementWidth;
            set
            {
                Set(ref _tiledElementWidth, value);
                ChangeCodecDimensions(TiledElementWidth, TiledElementHeight);
            }
        }

        private int _tiledElementHeight = 8;
        public int TiledElementHeight
        {
            get => _tiledElementHeight;
            set
            {
                Set(ref _tiledElementHeight, value);
                ChangeCodecDimensions(TiledElementWidth, TiledElementHeight);
            }
        }

        private int _tiledArrangerWidth = 8;
        public int TiledArrangerWidth
        {
            get => _tiledArrangerWidth;
            set
            {
                Set(ref _tiledArrangerWidth, value);
                ResizeArranger(TiledArrangerWidth, TiledArrangerHeight);
            }
        }

        private int _tiledArrangerHeight = 16;
        public int TiledArrangerHeight
        {
            get => _tiledArrangerHeight;
            set
            {
                Set(ref _tiledArrangerHeight, value);
                ResizeArranger(TiledArrangerWidth, TiledArrangerHeight);
            }
        }

        private int _linearArrangerWidth = 256;
        public int LinearArrangerWidth
        {
            get => _linearArrangerWidth;
            set
            {
                Set(ref _linearArrangerWidth, value);
                ChangeCodecDimensions(LinearArrangerWidth, LinearArrangerHeight);
            }
        }

        private int _linearArrangerHeight = 256;
        public int LinearArrangerHeight
        {
            get => _linearArrangerHeight;
            set
            {
                Set(ref _linearArrangerHeight, value);
                ChangeCodecDimensions(LinearArrangerWidth, LinearArrangerHeight);
            }
        }

        public SequentialArrangerEditorViewModel(SequentialArranger arranger, IEventAggregator events, ICodecService codecService)
        {
            Resource = arranger;
            _arranger = arranger;
            _events = events;
            _codecService = codecService;

            foreach (var name in codecService.GetSupportedCodecNames().OrderBy(x => x))
                CodecNames.Add(name);
            _selectedCodecName = arranger.ActiveCodec.Name;

            if(arranger.Layout == ArrangerLayout.TiledArranger)
            {
                _tiledElementWidth = arranger.ElementPixelSize.Width;
                _tiledElementHeight = arranger.ElementPixelSize.Height;
                _tiledArrangerHeight = arranger.ArrangerElementSize.Height;
                _tiledArrangerWidth = arranger.ArrangerElementSize.Width;
            }
            else if(arranger.Layout == ArrangerLayout.LinearArranger)
            {
                _linearArrangerHeight = arranger.ArrangerPixelSize.Height;
                _linearArrangerWidth = arranger.ArrangerPixelSize.Width;
            }

            Render();
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
        public void ExpandWidth()
        {
            if (IsTiledLayout)
                TiledArrangerWidth++;
            else
                LinearArrangerWidth += 8;
        }

        public void ExpandHeight()
        {
            if (IsTiledLayout)
                TiledArrangerHeight++;
            else
                LinearArrangerHeight += 8;
        }

        public void ShrinkWidth()
        {
            if (IsTiledLayout)
                TiledArrangerWidth = Math.Clamp(TiledArrangerWidth - 1, 1, int.MaxValue);
            else
                LinearArrangerHeight = Math.Clamp(LinearArrangerHeight - 8, 1, int.MaxValue);
        }

        public void ShrinkHeight()
        {
            if (IsTiledLayout)
                TiledArrangerHeight = Math.Clamp(TiledArrangerHeight - 1, 1, int.MaxValue);
            else
                LinearArrangerWidth = Math.Clamp(LinearArrangerWidth - 8, 1, int.MaxValue);
        }

        private void Move(ArrangerMoveType moveType)
        {
            var oldAddress = (_arranger as SequentialArranger).GetInitialSequentialFileAddress();
            var address = (_arranger as SequentialArranger).Move(moveType);

            if (oldAddress != address)
                Render();

            string notifyMessage = $"File Offset: 0x{address.FileOffset:X}";
            var notifyEvent = new NotifyStatusEvent(notifyMessage, NotifyStatusDuration.Indefinite);
            _events.PublishOnUIThreadAsync(notifyEvent);
        }

        private void ResizeArranger(int arrangerWidth, int arrangerHeight)
        {
            if (arrangerWidth <= 0 || arrangerHeight <= 0)
                return;

            if (arrangerWidth == _arranger.ArrangerElementSize.Width && 
                arrangerHeight == _arranger.ArrangerElementSize.Height && IsTiledLayout)
                return;

            if (arrangerWidth == _arranger.ArrangerPixelSize.Width &&
                arrangerHeight == _arranger.ArrangerPixelSize.Height && IsLinearLayout)
                return;

            (_arranger as SequentialArranger).Resize(arrangerWidth, arrangerHeight);
            Render();
        }

        private void ChangeCodec()
        {
            var codec = _codecService.CodecFactory.GetCodec(SelectedCodecName);
            if (codec.Layout == ImageMagitek.Codec.ImageLayout.Tiled)
            {
                _arranger.Resize(TiledArrangerWidth, TiledArrangerHeight);
                codec = _codecService.CodecFactory.GetCodec(SelectedCodecName, TiledElementWidth, TiledElementHeight);
                (_arranger as SequentialArranger).ChangeCodec(codec);
            }
            else if (codec.Layout == ImageMagitek.Codec.ImageLayout.Linear)
            {
                _arranger.Resize(1, 1);
                codec = _codecService.CodecFactory.GetCodec(SelectedCodecName, LinearArrangerWidth, LinearArrangerHeight);
                (_arranger as SequentialArranger).ChangeCodec(codec);
            }

            Render();

            NotifyOfPropertyChange(() => IsTiledLayout);
            NotifyOfPropertyChange(() => IsLinearLayout);
        }

        private void ChangeCodecDimensions(int width, int height)
        {
            var codec = _codecService.CodecFactory.GetCodec(SelectedCodecName, width, height);
            (_arranger as SequentialArranger).ChangeCodec(codec);
            Render();
        }

        private void Render()
        {
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
