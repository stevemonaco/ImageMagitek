using System;
using System.Linq;
using Stylet;
using ImageMagitek;
using TileShop.Shared.EventModels;
using TileShop.Shared.Services;
using TileShop.Shared.Models;
using TileShop.WPF.Behaviors;
using TileShop.WPF.Imaging;
using Jot;

namespace TileShop.WPF.ViewModels
{
    public class SequentialArrangerEditorViewModel : ArrangerEditorViewModel, IMouseCaptureProxy
    {
        private readonly ICodecService _codecService;
        private readonly Tracker _tracker;
        private FileBitAddress _address;

        private BindableCollection<string> _codecNames = new BindableCollection<string>();
        public BindableCollection<string> CodecNames
        {
            get => _codecNames;
            set => SetAndNotify(ref _codecNames, value);
        }

        private string _selectedCodecName;
        public string SelectedCodecName
        {
            get => _selectedCodecName;
            set
            {
                SetAndNotify(ref _selectedCodecName, value);
                ChangeCodec();
            }
        }

        private int _tiledElementWidth = 8;
        public int TiledElementWidth
        {
            get => _tiledElementWidth;
            set
            {
                SetAndNotify(ref _tiledElementWidth, value);
                ChangeCodecDimensions(TiledElementWidth, TiledElementHeight);
            }
        }

        private int _tiledElementHeight = 8;
        public int TiledElementHeight
        {
            get => _tiledElementHeight;
            set
            {
                SetAndNotify(ref _tiledElementHeight, value);
                ChangeCodecDimensions(TiledElementWidth, TiledElementHeight);
            }
        }

        private int _tiledArrangerWidth = 8;
        public int TiledArrangerWidth
        {
            get => _tiledArrangerWidth;
            set
            {
                SetAndNotify(ref _tiledArrangerWidth, value);
                ResizeArranger(TiledArrangerWidth, TiledArrangerHeight);
            }
        }

        private int _tiledArrangerHeight = 16;
        public int TiledArrangerHeight
        {
            get => _tiledArrangerHeight;
            set
            {
                SetAndNotify(ref _tiledArrangerHeight, value);
                ResizeArranger(TiledArrangerWidth, TiledArrangerHeight);
            }
        }

        private int _linearArrangerWidth = 256;
        public int LinearArrangerWidth
        {
            get => _linearArrangerWidth;
            set
            {
                SetAndNotify(ref _linearArrangerWidth, value);
                ChangeCodecDimensions(LinearArrangerWidth, LinearArrangerHeight);
            }
        }

        private int _linearArrangerHeight = 256;
        public int LinearArrangerHeight
        {
            get => _linearArrangerHeight;
            set
            {
                SetAndNotify(ref _linearArrangerHeight, value);
                ChangeCodecDimensions(LinearArrangerWidth, LinearArrangerHeight);
            }
        }

        public SequentialArrangerEditorViewModel(SequentialArranger arranger, IEventAggregator events, IWindowManager windowManager, 
            Tracker tracker, ICodecService codecService, IPaletteService paletteService) :
            base(events, windowManager, paletteService)
        {
            Resource = arranger;
            _workingArranger = arranger;
            _codecService = codecService;
            _tracker = tracker;
            DisplayName = Resource?.Name ?? "Unnamed Arranger";

            if (_workingArranger.ColorType == PixelColorType.Indexed)
                _indexedImage = new IndexedImage(_workingArranger, _defaultPalette);
            else if (_workingArranger.ColorType == PixelColorType.Direct)
                _directImage = new DirectImage(_workingArranger);

            foreach (var name in codecService.GetSupportedCodecNames().OrderBy(x => x))
                CodecNames.Add(name);
            _selectedCodecName = arranger.ActiveCodec.Name;

            if (arranger.Layout == ArrangerLayout.Tiled)
            {
                _tiledElementWidth = arranger.ElementPixelSize.Width;
                _tiledElementHeight = arranger.ElementPixelSize.Height;
                _tiledArrangerHeight = arranger.ArrangerElementSize.Height;
                _tiledArrangerWidth = arranger.ArrangerElementSize.Width;
                SnapMode = SnapMode.Element;
            }
            else if(arranger.Layout == ArrangerLayout.Single)
            {
                _linearArrangerHeight = arranger.ArrangerPixelSize.Height;
                _linearArrangerWidth = arranger.ArrangerPixelSize.Width;
                SnapMode = SnapMode.Pixel;
            }

            CreateGridlines();
            Render();
        }

        public override void SaveChanges()
        {
            IsModified = false;
        }

        public override void DiscardChanges()
        {
            IsModified = false;
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

        public void JumpToOffset()
        {
            var model = new JumpToOffsetViewModel();
            _tracker.Track(model);
            var result = _windowManager.ShowDialog(model);

            if (result is true)
            {
                Move(model.Result * 8);
                _tracker.Persist(model);
            }
        }

        private void Move(ArrangerMoveType moveType)
        {
            var oldAddress = (_workingArranger as SequentialArranger).GetInitialSequentialFileAddress();
            _address = (_workingArranger as SequentialArranger).Move(moveType);

            if (oldAddress != _address)
                Render();

            string notifyMessage = $"File Offset: 0x{_address.FileOffset:X}";
            var notifyEvent = new NotifyStatusEvent(notifyMessage, NotifyStatusDuration.Indefinite);
            _events.PublishOnUIThread(notifyEvent);
        }

        private void Move(long offset)
        {
            var oldAddress = (_workingArranger as SequentialArranger).GetInitialSequentialFileAddress();
            _address = (_workingArranger as SequentialArranger).Move(offset);

            if (oldAddress != _address)
                Render();

            string notifyMessage = $"File Offset: 0x{_address.FileOffset:X}";
            var notifyEvent = new NotifyStatusEvent(notifyMessage, NotifyStatusDuration.Indefinite);
            _events.PublishOnUIThread(notifyEvent);
        }

        private void ResizeArranger(int arrangerWidth, int arrangerHeight)
        {
            if (arrangerWidth <= 0 || arrangerHeight <= 0)
                return;

            if (arrangerWidth == _workingArranger.ArrangerElementSize.Width && 
                arrangerHeight == _workingArranger.ArrangerElementSize.Height && IsTiledLayout)
                return;

            if (arrangerWidth == _workingArranger.ArrangerPixelSize.Width &&
                arrangerHeight == _workingArranger.ArrangerPixelSize.Height && IsLinearLayout)
                return;

            (_workingArranger as SequentialArranger).Resize(arrangerWidth, arrangerHeight);
            Render();
        }

        public void SelectNextCodec()
        {
            var index = CodecNames.IndexOf(SelectedCodecName) + 1;
            SelectedCodecName = CodecNames[index % CodecNames.Count];
        }

        public void SelectPreviousCodec()
        {
            var index = CodecNames.IndexOf(SelectedCodecName) - 1;
            if (index < 0)
                index = CodecNames.Count - 1;
            SelectedCodecName = CodecNames[index];
        }

        private void ChangeCodec()
        {
            var codec = _codecService.CodecFactory.GetCodec(SelectedCodecName);
            if (codec.Layout == ImageMagitek.Codec.ImageLayout.Tiled)
            {
                _workingArranger.Resize(TiledArrangerWidth, TiledArrangerHeight);
                (_workingArranger as SequentialArranger).ChangeCodec(codec);
                SnapMode = SnapMode.Element;
            }
            else if (codec.Layout == ImageMagitek.Codec.ImageLayout.Single)
            {
                ShowGridlines = false;
                _workingArranger.Resize(1, 1);
                codec = _codecService.CodecFactory.GetCodec(SelectedCodecName, LinearArrangerWidth, LinearArrangerHeight);
                (_workingArranger as SequentialArranger).ChangeCodec(codec);
                SnapMode = SnapMode.Pixel;
            }

            Render();

            NotifyOfPropertyChange(() => IsTiledLayout);
            NotifyOfPropertyChange(() => IsLinearLayout);
            NotifyOfPropertyChange(() => CanShowGridlines);
        }

        private void ChangeCodecDimensions(int width, int height)
        {
            var codec = _codecService.CodecFactory.GetCodec(SelectedCodecName, width, height);
            (_workingArranger as SequentialArranger).ChangeCodec(codec);
            Render();
        }

        protected override void Render()
        {
            CancelOverlay();

            if (_workingArranger.ColorType == PixelColorType.Indexed)
            {
                _indexedImage.Render();
                ArrangerSource = new IndexedImageSource(_indexedImage, _workingArranger, _paletteService.DefaultPalette);
            }
            else if (_workingArranger.ColorType == PixelColorType.Direct)
            {
                _directImage.Render();
                ArrangerSource = new DirectImageSource(_directImage);
            }
        }

        public override void OnMouseMove(object sender, MouseCaptureArgs e)
        {
            if (Overlay.State == OverlayState.Selecting)
                Overlay.UpdateSelectionEndPoint(e.X / Zoom, e.Y / Zoom);

            if (Overlay.State == OverlayState.Selecting || Overlay.State == OverlayState.Selected)
            {
                string notifyMessage;
                var rect = Overlay.SelectionRect;
                if (rect.SnapMode == SnapMode.Element)
                    notifyMessage = $"Element Selection: {rect.SnappedWidth / _workingArranger.ElementPixelSize.Width} x {rect.SnappedHeight / _workingArranger.ElementPixelSize.Height}" +
                        $" at ({rect.SnappedLeft / _workingArranger.ElementPixelSize.Width}, {rect.SnappedRight / _workingArranger.ElementPixelSize.Height})";
                else
                    notifyMessage = $"Pixel Selection: {rect.SnappedWidth} x {rect.SnappedHeight}" +
                        $" at ({rect.SnappedLeft} x {rect.SnappedTop})";
                var notifyEvent = new NotifyStatusEvent(notifyMessage, NotifyStatusDuration.Indefinite);
                _events.PublishOnUIThread(notifyEvent);
            }
            else
            {
                string notifyMessage = $"File Offset: 0x{_address.FileOffset:X} ({(int)Math.Round(e.X / Zoom)}, {(int)Math.Round(e.Y / Zoom)})";
                var notifyEvent = new NotifyStatusEvent(notifyMessage, NotifyStatusDuration.Indefinite);
                _events.PublishOnUIThread(notifyEvent);
            }
        }

        /// <summary>
        /// Checks if the specified arranger can be copied into the current SequentialArranger
        /// SequentialArrangers can only copy pixels
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        protected override bool CanAcceptTransfer(ArrangerTransferModel model)
        {
            CanPastePixels = true;
            CanPasteElements = false;
            return true;
        }
    }
}
