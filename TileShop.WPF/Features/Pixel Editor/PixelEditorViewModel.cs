using Stylet;
using ImageMagitek.Services;
using TileShop.WPF.Behaviors;
using TileShop.WPF.Models;
using ImageMagitek;
using System;

namespace TileShop.WPF.ViewModels
{
    public enum PixelTool { Select, Pencil, ColorPicker, FloodFill }
    public enum ColorPriority { Primary, Secondary }

    public abstract class PixelEditorViewModel<TColor> : ArrangerEditorViewModel
        where TColor : struct
    {
        protected readonly Arranger _projectArranger;
        protected int _viewX;
        protected int _viewY;
        protected int _viewWidth;
        protected int _viewHeight;
        protected PencilHistoryAction<TColor> _activePencilHistory;

        public Arranger SourceArranger { get; }

        private bool _isDrawing;
        public bool IsDrawing
        {
            get => _isDrawing;
            set => SetAndNotify(ref _isDrawing, value);
        }

        private PixelTool _activeTool = PixelTool.Pencil;
        public PixelTool ActiveTool
        {
            get => _activeTool;
            set => SetAndNotify(ref _activeTool, value);
        }

        private TColor _activeColor;
        public TColor ActiveColor
        {
            get => _activeColor;
            set => SetAndNotify(ref _activeColor, value);
        }

        private TColor _primaryColor;
        public TColor PrimaryColor
        {
            get => _primaryColor;
            set => SetAndNotify(ref _primaryColor, value);
        }

        private TColor _secondaryColor;
        public TColor SecondaryColor
        {
            get => _secondaryColor;
            set => SetAndNotify(ref _secondaryColor, value);
        }

        public PixelEditorViewModel(Arranger projectArranger, IEventAggregator events, IWindowManager windowManager, IPaletteService paletteService) :
            base(events, windowManager, paletteService)
        {
            DisplayName = "Pixel Editor";
            CanAcceptElementPastes = true;
            CanAcceptPixelPastes = true;

            Zoom = 3;
            MaxZoom = 32;
            OriginatingProjectResource = projectArranger;
            _projectArranger = projectArranger;
        }

        protected abstract void ReloadImage();
        public abstract void SetPixel(int x, int y, TColor color);
        public abstract TColor GetPixel(int x, int y);
        public abstract void FloodFill(int x, int y, TColor fillColor);

        public void SetPrimaryColor(TColor color) => PrimaryColor = color;
        public void SetSecondaryColor(TColor color) => SecondaryColor = color;

        public override void Undo()
        {
            if (!CanUndo)
                return;

            var lastAction = UndoHistory[^1];
            UndoHistory.RemoveAt(UndoHistory.Count - 1);
            RedoHistory.Add(lastAction);
            NotifyOfPropertyChange(() => CanUndo);
            NotifyOfPropertyChange(() => CanRedo);

            IsModified = UndoHistory.Count > 0;

            ReloadImage();

            foreach (var action in UndoHistory)
                ApplyHistoryAction(action);

            Render();
        }

        public override void Redo()
        {
            if (!CanRedo)
                return;

            var redoAction = RedoHistory[^1];
            RedoHistory.RemoveAt(RedoHistory.Count - 1);
            UndoHistory.Add(redoAction);
            NotifyOfPropertyChange(() => CanUndo);
            NotifyOfPropertyChange(() => CanRedo);

            ApplyHistoryAction(redoAction);
            IsModified = true;
            Render();
        }

        #region Commands
        public virtual void StartDraw(int x, int y, ColorPriority priority)
        {
            if (priority == ColorPriority.Primary)
                _activePencilHistory = new PencilHistoryAction<TColor>(PrimaryColor);
            else if (priority == ColorPriority.Secondary)
                _activePencilHistory = new PencilHistoryAction<TColor>(SecondaryColor);
            IsDrawing = true;
        }

        /// <summary>
        /// Pick a color at the specified coordinate
        /// </summary>
        /// <param name="x">x-coordinate in pixel coordinates</param>
        /// <param name="y">y-coordinate in pixel coordinates</param>
        /// <param name="priority">Priority to apply the color pick to</param>
        public virtual void PickColor(int x, int y, ColorPriority priority)
        {
            var color = GetPixel(x, y);

            if (priority == ColorPriority.Primary)
                PrimaryColor = color;
            else if (priority == ColorPriority.Secondary)
                SecondaryColor = color;
        }
        #endregion

        #region Mouse Actions
        public override void OnMouseDown(object sender, MouseCaptureArgs e)
        {
            int x = Math.Clamp((int)e.X / Zoom, 0, WorkingArranger.ArrangerPixelSize.Width - 1);
            int y = Math.Clamp((int)e.Y / Zoom, 0, WorkingArranger.ArrangerPixelSize.Height - 1);

            // Always drag first
            if (ActiveTool != PixelTool.Select && Paste?.Rect.ContainsPointSnapped(x, y) is true)
                return;

            if (ActiveTool == PixelTool.Pencil && e.LeftButton)
            {
                StartDraw(x, y, ColorPriority.Primary);
                SetPixel(x, y, PrimaryColor);
            }
            else if (ActiveTool == PixelTool.Pencil && e.RightButton)
            {
                StartDraw(x, y, ColorPriority.Secondary);
                SetPixel(x, y, SecondaryColor);
            }
            else if (ActiveTool == PixelTool.ColorPicker && e.LeftButton)
            {
                PickColor(x, y, ColorPriority.Primary);
            }
            else if (ActiveTool == PixelTool.ColorPicker && e.RightButton)
            {
                PickColor(x, y, ColorPriority.Secondary);
            }
            else if (ActiveTool == PixelTool.FloodFill && e.LeftButton)
            {
                FloodFill(x, y, PrimaryColor);
            }
            else if (ActiveTool == PixelTool.FloodFill && e.RightButton)
            {
                FloodFill(x, y, SecondaryColor);
            }
            else if (ActiveTool == PixelTool.Select)
            {
                base.OnMouseDown(sender, e);
            }
        }

        public override void OnMouseLeave(object sender, MouseCaptureArgs e)
        {
            if (ActiveTool == PixelTool.Pencil && IsDrawing && _activePencilHistory?.ModifiedPoints.Count > 0)
            {
                IsDrawing = false;
                AddHistoryAction(_activePencilHistory);
            }
            else
                base.OnMouseLeave(sender, e);
        }

        public override void OnMouseMove(object sender, MouseCaptureArgs e)
        {
            int x = Math.Clamp((int)e.X / Zoom, 0, WorkingArranger.ArrangerPixelSize.Width - 1);
            int y = Math.Clamp((int)e.Y / Zoom, 0, WorkingArranger.ArrangerPixelSize.Height - 1);

            if (x < 0 || x >= WorkingArranger.ArrangerPixelSize.Width || y < 0 || y >= WorkingArranger.ArrangerPixelSize.Height)
                return;

            if (IsDrawing && ActiveTool == PixelTool.Pencil && e.LeftButton)
                SetPixel(x, y, PrimaryColor);
            else if (IsDrawing && ActiveTool == PixelTool.Pencil && e.RightButton)
                SetPixel(x, y, SecondaryColor);
            else
                base.OnMouseMove(sender, e);
        }

        public override void OnMouseUp(object sender, MouseCaptureArgs e)
        {
            if (ActiveTool == PixelTool.Pencil && IsDrawing && _activePencilHistory?.ModifiedPoints.Count > 0)
            {
                IsDrawing = false;
                AddHistoryAction(_activePencilHistory);
                _activePencilHistory = null;
            }
            else
                base.OnMouseUp(sender, e);
        }
        #endregion
    }
}
