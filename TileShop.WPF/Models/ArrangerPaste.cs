using ImageMagitek;
using Stylet;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;

namespace TileShop.WPF.Models
{
    public enum PasteMode { Elements, Pixels }
    public class ArrangerPaste : Screen
    {
        private IndexedImage _sourceIndexed;
        private DirectImage _sourceDirect;
        private Arranger _sourceArranger;

        private IndexedImage _destIndexed;
        private DirectImage _destDirect;
        private Arranger _destArranger;

        private bool _hasPaste;
        public bool HasPaste
        {
            get => _hasPaste;
            set => SetAndNotify(ref _hasPaste, value);
        }

        private PasteMode _pasteMode;
        public PasteMode PasteMode
        {
            get => _pasteMode;
            set => SetAndNotify(ref _pasteMode, value);
        }

        private int _sourceX;
        public int SourceX
        {
            get => _sourceX;
            set => SetAndNotify(ref _sourceX, value);
        }

        private int _sourceY;
        public int SourceY
        {
            get => _sourceY;
            set => SetAndNotify(ref _sourceY, value);
        }

        private int _destX;
        public int DestX
        {
            get => _destX;
            set => SetAndNotify(ref _destX, value);
        }

        private int _destY;
        public int DestY
        {
            get => _destY;
            set => SetAndNotify(ref _destY, value);
        }

        private int _maxWidth;
        public int MaxWidth
        {
            get => _maxWidth;
            set => SetAndNotify(ref _maxWidth, value);
        }

        private int _maxHeight;
        public int MaxHeight
        {
            get => _maxHeight;
            set => SetAndNotify(ref _maxHeight, value);
        }

        private int _copyWidth;
        public int CopyWidth
        {
            get => _copyWidth;
            set => SetAndNotify(ref _copyWidth, value);
        }

        private int _copyHeight;
        public int CopyHeight
        {
            get => _copyHeight;
            set => SetAndNotify(ref _copyHeight, value);
        }

        public ArrangerPaste()
        {
        }

        public ArrangerPaste(IndexedImage source, int sourceX, int sourceY, int maxWidth, int maxHeight)
        {
            _sourceIndexed = source;
            SourceX = sourceX;
            SourceY = sourceY;
            MaxWidth = maxWidth;
            MaxHeight = maxHeight;
        }

        public void StartPaste(Arranger source, int sourceX, int sourceY, int maxWidth, int maxHeight)
        {
            _sourceArranger = source;
            SourceX = sourceX;
            SourceY = sourceY;
            MaxWidth = maxWidth;
            MaxHeight = maxHeight;

            CopyWidth = MaxWidth;
            CopyHeight = MaxHeight;

            HasPaste = true;
        }

        public void CancelPaste()
        {
            _destIndexed = null;
            _sourceIndexed = null;
            _destDirect = null;
            _sourceDirect = null;
            _sourceArranger = null;
            _destArranger = null;

            HasPaste = false;
        }

        public void SetDestinationStart(Arranger dest, int x, int y)
        {
            DestX = x;
            DestY = y;
            _destArranger = dest;
        }

        public bool CanApply()
        {
            var sourcePoint = new Point(SourceX, SourceY);
            var destPoint = new Point(DestX, DestY);
            if (_destArranger is ScatteredArranger destArr)
                return ElementCopier.CanCopyElements(_sourceArranger, destArr, sourcePoint, destPoint, CopyWidth, CopyHeight);
            else
                return false;
        }

        public void Apply()
        {
            var sourcePoint = new Point(SourceX, SourceY);
            var destPoint = new Point(DestX, DestY);

            if (_destArranger is ScatteredArranger destArr)
                ElementCopier.CopyElements(_sourceArranger, destArr, sourcePoint, destPoint, CopyWidth, CopyHeight);
            else
                throw new InvalidOperationException($"{nameof(Apply)} must operate on a destination arranger that is scattered");
        }
    }
}
