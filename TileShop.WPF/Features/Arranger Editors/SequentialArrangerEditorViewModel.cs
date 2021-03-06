﻿using System;
using System.Linq;
using Stylet;
using ImageMagitek;
using ImageMagitek.Services;
using TileShop.Shared.Models;
using TileShop.WPF.Behaviors;
using TileShop.WPF.Imaging;
using TileShop.WPF.EventModels;
using Jot;
using TileShop.WPF.Models;
using System.Drawing;

namespace TileShop.WPF.ViewModels
{
    public class SequentialArrangerEditorViewModel : ArrangerEditorViewModel, IMouseCaptureProxy
    {
        private readonly ICodecService _codecService;
        private readonly Tracker _tracker;
        private IndexedImage _indexedImage;
        private DirectImage _directImage;

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

        private BindableCollection<PaletteModel> _palettes = new BindableCollection<PaletteModel>();
        public BindableCollection<PaletteModel> Palettes
        {
            get => _palettes;
            set => SetAndNotify(ref _palettes, value);
        }

        private PaletteModel _selectedPalette;
        public PaletteModel SelectedPalette
        {
            get => _selectedPalette;
            set
            {
                SetAndNotify(ref _selectedPalette, value);
                ChangePalette(SelectedPalette);
            }
        }

        private int _tiledElementWidth = 8;
        public int TiledElementWidth
        {
            get => _tiledElementWidth;
            set
            {
                var preferredWidth = (WorkingArranger as SequentialArranger).ActiveCodec.GetPreferredWidth(value);
                SetAndNotify(ref _tiledElementWidth, preferredWidth);
                ChangeCodecDimensions(TiledElementWidth, TiledElementHeight);
            }
        }

        private int _tiledElementHeight = 8;
        public int TiledElementHeight
        {
            get => _tiledElementHeight;
            set
            {
                var preferredHeight = (WorkingArranger as SequentialArranger).ActiveCodec.GetPreferredHeight(value);
                SetAndNotify(ref _tiledElementHeight, preferredHeight);
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
                var preferredWidth = (WorkingArranger as SequentialArranger).ActiveCodec.GetPreferredWidth(value);
                SetAndNotify(ref _linearArrangerWidth, preferredWidth);
                ChangeCodecDimensions(LinearArrangerWidth, LinearArrangerHeight);
            }
        }

        private int _linearArrangerHeight = 256;
        public int LinearArrangerHeight
        {
            get => _linearArrangerHeight;
            set
            {
                var preferredHeight = (WorkingArranger as SequentialArranger).ActiveCodec.GetPreferredHeight(value);
                SetAndNotify(ref _linearArrangerHeight, preferredHeight);
                ChangeCodecDimensions(LinearArrangerWidth, LinearArrangerHeight);
            }
        }

        private bool _canResize;
        public bool CanResize
        {
            get => _canResize;
            set => SetAndNotify(ref _canResize, value);
        }

        private int _widthIncrement = 1;
        public int WidthIncrement
        {
            get => _widthIncrement;
            set => SetAndNotify(ref _widthIncrement, value);
        }

        private int _heightIncrement = 1;
        public int HeightIncrement
        {
            get => _heightIncrement;
            set => SetAndNotify(ref _heightIncrement, value);
        }

        private long _fileOffset;
        public long FileOffset
        {
            get => _fileOffset;
            set
            {
                if (SetAndNotify(ref _fileOffset, value))
                    Move(_fileOffset);
            }
        }

        private long _maxFileDecodingOffset;
        public long MaxFileDecodingOffset
        {
            get => _maxFileDecodingOffset;
            set => SetAndNotify(ref _maxFileDecodingOffset, value);
        }

        private int _arrangerPageSize;
        public int ArrangerPageSize
        {
            get => _arrangerPageSize;
            set => SetAndNotify(ref _arrangerPageSize, value);
        }

        public SequentialArrangerEditorViewModel(SequentialArranger arranger, IEventAggregator events, IWindowManager windowManager, 
            Tracker tracker, ICodecService codecService, IPaletteService paletteService) :
            base(events, windowManager, paletteService)
        {
            Resource = arranger;
            WorkingArranger = arranger;
            _codecService = codecService;
            _tracker = tracker;
            DisplayName = Resource?.Name ?? "Unnamed Arranger";

            CreateImages();

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

            CanChangeSnapMode = true;
            CanResize = arranger.ActiveCodec.CanResize;
            WidthIncrement = arranger.ActiveCodec.WidthResizeIncrement;
            HeightIncrement = arranger.ActiveCodec.HeightResizeIncrement;

            Palettes = new BindableCollection<PaletteModel>(_paletteService.GlobalPalettes.Select(x => new PaletteModel(x)));
            SelectedPalette = Palettes.First();

            ArrangerPageSize = (int) (WorkingArranger as SequentialArranger).ArrangerBitSize / 8;
            MaxFileDecodingOffset = (WorkingArranger as SequentialArranger).FileSize - ArrangerPageSize;
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
                LinearArrangerWidth += WidthIncrement;
        }

        public void ExpandHeight()
        {
            if (IsTiledLayout)
                TiledArrangerHeight++;
            else
                LinearArrangerHeight += HeightIncrement;
        }

        public void ShrinkWidth()
        {
            if (IsTiledLayout)
                TiledArrangerWidth = Math.Clamp(TiledArrangerWidth - 1, 1, int.MaxValue);
            else
                LinearArrangerHeight = Math.Clamp(LinearArrangerHeight - WidthIncrement, WidthIncrement, int.MaxValue);
        }

        public void ShrinkHeight()
        {
            if (IsTiledLayout)
                TiledArrangerHeight = Math.Clamp(TiledArrangerHeight - 1, 1, int.MaxValue);
            else
                LinearArrangerWidth = Math.Clamp(LinearArrangerWidth - HeightIncrement, HeightIncrement, int.MaxValue);
        }

        public void JumpToOffset()
        {
            var model = new JumpToOffsetViewModel();
            _tracker.Track(model);
            var result = _windowManager.ShowDialog(model);

            if (result is true)
            {
                Move(model.Result);
                _tracker.Persist(model);
            }
        }

        public void NewScatteredArrangerFromSelection()
        {
            if (SnapMode == SnapMode.Element)
            {
                int x = Selection.SelectionRect.SnappedLeft / WorkingArranger.ElementPixelSize.Width;
                int y = Selection.SelectionRect.SnappedTop / WorkingArranger.ElementPixelSize.Height;
                int width = Selection.SelectionRect.SnappedWidth / WorkingArranger.ElementPixelSize.Width;
                int height = Selection.SelectionRect.SnappedHeight / WorkingArranger.ElementPixelSize.Height;

                var copy = new ElementCopy(WorkingArranger, x, y, width, height);
                var model = new AddScatteredArrangerFromCopyEvent(copy, OriginatingProjectResource);
                _events.PublishOnUIThread(model);
            }
            else
            {
                _windowManager.ShowMessageBox("Selection must be performed in Element Snap mode to create a new Scattered Arranger", "Error");
            }
        }

        public void NewScatteredArrangerFromImage()
        {
            if (SnapMode == SnapMode.Element)
            {
                var copy = new ElementCopy(WorkingArranger, 0, 0, 1, 1);
                var model = new AddScatteredArrangerFromCopyEvent(copy, OriginatingProjectResource);
                _events.PublishOnUIThread(model);
            }
            else
            {
                _windowManager.ShowMessageBox("Selection must be performed in Element Snap mode to create a new Scattered Arranger", "Error");
            }
        }

        private void Move(ArrangerMoveType moveType)
        {
            var oldAddress = (WorkingArranger as SequentialArranger).GetInitialSequentialFileAddress();
            var newAddress = (WorkingArranger as SequentialArranger).Move(moveType);

            if (oldAddress != newAddress)
            {
                _fileOffset = newAddress.FileOffset;
                NotifyOfPropertyChange(() => FileOffset);
                Render();
            }
        }

        private void Move(long offset)
        {
            var oldAddress = (WorkingArranger as SequentialArranger).GetInitialSequentialFileAddress();
            var newAddress = (WorkingArranger as SequentialArranger).Move(new FileBitAddress(offset, 0));

            if (oldAddress != newAddress)
            {
                _fileOffset = newAddress.FileOffset;
                NotifyOfPropertyChange(() => FileOffset);
                Render();
            }
        }

        private void ResizeArranger(int arrangerWidth, int arrangerHeight)
        {
            if (arrangerWidth <= 0 || arrangerHeight <= 0)
                return;

            if (arrangerWidth == WorkingArranger.ArrangerElementSize.Width && 
                arrangerHeight == WorkingArranger.ArrangerElementSize.Height && IsTiledLayout)
                return;

            if (arrangerWidth == WorkingArranger.ArrangerPixelSize.Width &&
                arrangerHeight == WorkingArranger.ArrangerPixelSize.Height && IsSingleLayout)
                return;

            (WorkingArranger as SequentialArranger).Resize(arrangerWidth, arrangerHeight);
            CreateImages();
            ArrangerPageSize = (int)(WorkingArranger as SequentialArranger).ArrangerBitSize / 8;
            MaxFileDecodingOffset = (WorkingArranger as SequentialArranger).FileSize - ArrangerPageSize;
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

        public void ToggleSnapMode()
        {
            if (SnapMode == SnapMode.Element)
                SnapMode = SnapMode.Pixel;
            else if (SnapMode == SnapMode.Pixel)
                SnapMode = SnapMode.Element;
        }

        private void ChangeCodec()
        {
            var codec = _codecService.CodecFactory.GetCodec(SelectedCodecName, default);
            if (codec.Layout == ImageMagitek.Codec.ImageLayout.Tiled)
            {
                _tiledElementHeight = codec.Height;
                _tiledElementWidth = codec.Width;

                (WorkingArranger as SequentialArranger).ChangeCodec(codec, TiledArrangerWidth, TiledArrangerHeight);
                SnapMode = SnapMode.Element;

                NotifyOfPropertyChange(() => TiledElementHeight);
                NotifyOfPropertyChange(() => TiledElementWidth);
            }
            else if (codec.Layout == ImageMagitek.Codec.ImageLayout.Single)
            {
                ShowGridlines = false;
                _linearArrangerHeight = codec.Height;
                _linearArrangerWidth = codec.Width;

                (WorkingArranger as SequentialArranger).ChangeCodec(codec, 1, 1);
                SnapMode = SnapMode.Pixel;

                NotifyOfPropertyChange(() => LinearArrangerHeight);
                NotifyOfPropertyChange(() => LinearArrangerWidth);
            }

            _fileOffset = (WorkingArranger as SequentialArranger).FileAddress;
            ArrangerPageSize = (int)(WorkingArranger as SequentialArranger).ArrangerBitSize / 8;
            MaxFileDecodingOffset = (WorkingArranger as SequentialArranger).FileSize - ArrangerPageSize;
            CanResize = codec.CanResize;
            WidthIncrement = codec.WidthResizeIncrement;
            HeightIncrement = codec.HeightResizeIncrement;
            CreateImages();

            NotifyOfPropertyChange(() => FileOffset);
            NotifyOfPropertyChange(() => IsTiledLayout);
            NotifyOfPropertyChange(() => IsSingleLayout);
        }

        private void ChangePalette(PaletteModel pal)
        {
            (WorkingArranger as SequentialArranger).ChangePalette(pal.Palette);
            Render();
        }

        private void ChangeCodecDimensions(int width, int height)
        {
            var codec = _codecService.CodecFactory.GetCodec(SelectedCodecName, new Size(width, height));
            (WorkingArranger as SequentialArranger).ChangeCodec(codec);
            CreateImages();
        }

        private void CreateImages()
        {
            CancelOverlay();

            if (WorkingArranger.ColorType == PixelColorType.Indexed)
            {
                _indexedImage = new IndexedImage(WorkingArranger);
                BitmapAdapter = new IndexedBitmapAdapter(_indexedImage);
            }
            else if (WorkingArranger.ColorType == PixelColorType.Direct)
            {
                _directImage = new DirectImage(WorkingArranger);
                BitmapAdapter = new DirectBitmapAdapter(_directImage);
            }

            CreateGridlines();
        }

        protected override void CreateGridlines()
        {
            if (WorkingArranger.Layout == ArrangerLayout.Single)
            {
                CreateGridlines(0, 0, WorkingArranger.ArrangerPixelSize.Width, WorkingArranger.ArrangerPixelSize.Height, 8, 8);
            }
            else if (WorkingArranger.Layout == ArrangerLayout.Tiled)
            {
                base.CreateGridlines();
            }
        }

        public override void Render()
        {
            CancelOverlay();

            if (WorkingArranger.ColorType == PixelColorType.Indexed)
            {
                _indexedImage.Render();
                BitmapAdapter.Invalidate();
            }
            else if (WorkingArranger.ColorType == PixelColorType.Direct)
            {
                _directImage.Render();
                BitmapAdapter.Invalidate();
            }
        }

        #region Unsupported Operations due to SequentialArrangerEditor being read-only
        public override void Undo()
        {
            throw new NotSupportedException("Sequential Arrangers are read-only");
        }

        public override void Redo()
        {
            throw new NotSupportedException("Sequential Arrangers are read-only");
        }

        public override void ApplyHistoryAction(HistoryAction action)
        {
            throw new NotSupportedException("Sequential Arrangers are read-only");
        }

        public override void ApplyPaste(ArrangerPaste paste)
        {
            throw new NotSupportedException("Sequential Arrangers are read-only");
        }
        #endregion
    }
}
