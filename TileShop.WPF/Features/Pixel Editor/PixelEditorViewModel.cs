using Stylet;
using ImageMagitek.Services;
using TileShop.WPF.Behaviors;
using TileShop.WPF.Models;

namespace TileShop.WPF.ViewModels
{
    public enum PixelTool { Pencil, ColorPicker }

    public abstract class PixelEditorViewModel<TColor> : ArrangerEditorViewModel
        where TColor : struct
    {
        protected int _viewX;
        protected int _viewY;
        protected int _viewWidth;
        protected int _viewHeight;
        protected PencilHistoryAction<TColor> _activePencilHistory;

        private BindableCollection<HistoryAction> _undoHistory = new BindableCollection<HistoryAction>();
        public BindableCollection<HistoryAction> UndoHistory
        {
            get => _undoHistory;
            set => SetAndNotify(ref _undoHistory, value);
        }

        private BindableCollection<HistoryAction> _redoHistory = new BindableCollection<HistoryAction>();
        public BindableCollection<HistoryAction> RedoHistory
        {
            get => _redoHistory;
            set => SetAndNotify(ref _redoHistory, value);
        }

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

        public PixelEditorViewModel(IEventAggregator events, IWindowManager windowManager, IPaletteService paletteService) :
            base(events, windowManager, paletteService)
        {
            DisplayName = "Pixel Editor";

            Zoom = 3;
            MaxZoom = 32;
        }

        protected abstract void ReloadImage();
        public abstract void SetPixel(int x, int y, TColor color);
        public abstract TColor GetPixel(int x, int y);

        public void SetPrimaryColor(TColor color) => PrimaryColor = color;
        public void SetSecondaryColor(TColor color) => SecondaryColor = color;

        public abstract void ApplyAction(HistoryAction action);

        public bool CanUndo { get => UndoHistory.Count > 0; }
        public bool CanRedo { get => RedoHistory.Count > 0; }

        public virtual void AddHistoryAction(HistoryAction action)
        {
            UndoHistory.Add(action);
            RedoHistory.Clear();
            NotifyOfPropertyChange(() => CanUndo);
            NotifyOfPropertyChange(() => CanRedo);
        }

        public virtual void Undo()
        {
            var lastAction = UndoHistory[^1];
            UndoHistory.RemoveAt(UndoHistory.Count - 1);
            RedoHistory.Add(lastAction);
            NotifyOfPropertyChange(() => CanUndo);
            NotifyOfPropertyChange(() => CanRedo);

            IsModified = UndoHistory.Count > 0;

            ReloadImage();

            foreach (var action in UndoHistory)
                ApplyAction(action);

            Render();
        }

        public virtual void Redo()
        {
            var redoAction = RedoHistory[^1];
            RedoHistory.RemoveAt(RedoHistory.Count - 1);
            UndoHistory.Add(redoAction);
            NotifyOfPropertyChange(() => CanUndo);
            NotifyOfPropertyChange(() => CanRedo);

            ApplyAction(redoAction);
            IsModified = true;
            Render();
        }

        public override void OnMouseDown(object sender, MouseCaptureArgs e)
        {
            int x = (int)e.X / Zoom;
            int y = (int)e.Y / Zoom;

            if (ActiveTool == PixelTool.Pencil && e.LeftButton)
            {
                _activePencilHistory = new PencilHistoryAction<TColor>(PrimaryColor);
                SetPixel(x, y, PrimaryColor);
                IsDrawing = true;
            }
            else if (ActiveTool == PixelTool.Pencil && e.RightButton)
            {
                _activePencilHistory = new PencilHistoryAction<TColor>(SecondaryColor);
                SetPixel(x, y, SecondaryColor);
                IsDrawing = true;
            }
            else if (ActiveTool == PixelTool.ColorPicker && e.LeftButton)
            {
                PrimaryColor = GetPixel(x, y);
                ActiveColor = PrimaryColor;
            }
            else if (ActiveTool == PixelTool.ColorPicker && e.RightButton)
            {
                SecondaryColor = GetPixel(x, y);
                ActiveColor = SecondaryColor;
            }
        }

        public override void OnMouseLeave(object sender, MouseCaptureArgs e)
        {
            if (ActiveTool == PixelTool.Pencil && IsDrawing && _activePencilHistory?.ModifiedPoints.Count > 0)
            {
                IsDrawing = false;
                AddHistoryAction(_activePencilHistory);
            }
        }

        public override void OnMouseMove(object sender, MouseCaptureArgs e)
        {
            int x = (int)e.X / Zoom;
            int y = (int)e.Y / Zoom;

            if (x < 0 || x >= _workingArranger.ArrangerPixelSize.Width || y < 0 || y >= _workingArranger.ArrangerPixelSize.Height)
                return;

            if (IsDrawing && ActiveTool == PixelTool.Pencil && e.LeftButton)
                SetPixel(x, y, PrimaryColor);
            else if (IsDrawing && ActiveTool == PixelTool.Pencil && e.RightButton)
                SetPixel(x, y, SecondaryColor);
        }

        public override void OnMouseUp(object sender, MouseCaptureArgs e)
        {
            if (ActiveTool == PixelTool.Pencil && IsDrawing && _activePencilHistory?.ModifiedPoints.Count > 0)
            {
                IsDrawing = false;
                AddHistoryAction(_activePencilHistory);
                _activePencilHistory = null;
            }
        }
    }
}
