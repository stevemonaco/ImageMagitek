﻿using Caliburn.Micro;
using ImageMagitek;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using TileShop.Shared.EventModels;
using TileShop.WPF.Behaviors;
using TileShop.WPF.Helpers;
using TileShop.WPF.Models;
using TileShop.WPF.Services;
using Point = System.Drawing.Point;

namespace TileShop.WPF.ViewModels
{
    public enum PixelTool { Pencil, ColorPicker }

    public class PixelEditorViewModel : ArrangerEditorViewModel, IMouseCaptureProxy, IHandle<EditArrangerPixelsEvent>
    {
        private IUserPromptService _promptService;
        private int _cropX;
        private int _cropY;
        private int _cropWidth;
        private int _cropHeight;
        private PencilHistoryAction _activePencilHistory;

        public override string Name => "Pixel Editor";

        private BindableCollection<HistoryAction> _history = new BindableCollection<HistoryAction>();
        public BindableCollection<HistoryAction> History
        {
            get => _history;
            set => Set(ref _history, value);
        }

        private bool _hasArranger;
        public bool HasArranger
        {
            get => _hasArranger;
            set => Set(ref _hasArranger, value);
        }

        private bool _isDrawing;
        public bool IsDrawing
        {
            get => _isDrawing;
            set => Set(ref _isDrawing, value);
        }

        private PixelTool _activeTool = PixelTool.Pencil;
        public PixelTool ActiveTool
        {
            get => _activeTool;
            set => Set(ref _activeTool, value);
        }

        private Color _activeColor = Color.FromRgb(0, 255, 0);
        public Color ActiveColor
        {
            get => _activeColor;
            set => Set(ref _activeColor, value);
        }

        private Color _primaryColor = Color.FromRgb(0, 255, 0);
        public Color PrimaryColor
        {
            get => _primaryColor;
            set => Set(ref _primaryColor, value);
        }

        private Color _secondaryColor = Color.FromRgb(0, 0, 255);
        public Color SecondaryColor
        {
            get => _secondaryColor;
            set => Set(ref _secondaryColor, value);
        }

        public override bool CanShowGridlines => HasArranger;

        public PixelEditorViewModel(IEventAggregator events, IUserPromptService promptService)
        {
            _events = events;
            _promptService = promptService;

            _events.SubscribeOnUIThread(this);
        }

        public void CreateImage()
        {
            _arrangerImage = new ArrangerImage();
            _arrangerImage.RenderSubImage(_arranger, _cropX, _cropY, _cropWidth, _cropHeight);
            ArrangerSource = new ImageRgba32Source(_arrangerImage.Image);
            HasArranger = true;
        }

        public void Render()
        {
            if(HasArranger)
                ArrangerSource = new ImageRgba32Source(_arrangerImage.Image);
        }

        public void SetPixel(int x, int y, Color color)
        {
            var arrangerColor = new Rgba32(color.R, color.G, color.B, color.A);
            _arrangerImage.SetPixel(x, y, arrangerColor);
            Render();
        }

        public Color GetPixel(int x, int y)
        {
            var arrangerColor = _arrangerImage.GetPixel(x, y);
            var newColor = new Color();
            newColor.R = arrangerColor.R;
            newColor.G = arrangerColor.G;
            newColor.B = arrangerColor.B;
            newColor.A = arrangerColor.A;
            return newColor; 
        }

        public override void OnMouseDown(object sender, MouseCaptureArgs e)
        {
            int x = (int)e.X / Zoom;
            int y = (int)e.Y / Zoom;

            if(ActiveTool == PixelTool.Pencil && e.LeftButton)
            {
                SetPixel(x, y, PrimaryColor);
                _activePencilHistory = new PencilHistoryAction();
                _activePencilHistory.ModifiedPoints.Add(new Point(x, y));
                IsDrawing = true;
            }
            else if(ActiveTool == PixelTool.Pencil && e.RightButton)
            {
                SetPixel(x, y, SecondaryColor);
                _activePencilHistory = new PencilHistoryAction();
                _activePencilHistory.ModifiedPoints.Add(new Point(x, y));
                IsDrawing = true;
            }
            else if(ActiveTool == PixelTool.ColorPicker && e.LeftButton)
            {
                PrimaryColor = GetPixel(x, y);
                ActiveColor = PrimaryColor;
            }
            else if(ActiveTool == PixelTool.ColorPicker && e.RightButton)
            {
                SecondaryColor = GetPixel(x, y);
                ActiveColor = SecondaryColor;
            }
        }

        public override void OnMouseLeave(object sender, MouseCaptureArgs e)
        {

        }

        public override void OnMouseMove(object sender, MouseCaptureArgs e)
        {
            int x = (int)e.X / Zoom;
            int y = (int)e.Y / Zoom;

            if (IsDrawing && ActiveTool == PixelTool.Pencil && e.LeftButton)
            {
                if(_activePencilHistory.Add(x, y))
                {
                    SetPixel(x, y, PrimaryColor);
                }
            }
            else if(IsDrawing && ActiveTool == PixelTool.Pencil && e.RightButton)
            {
                if (_activePencilHistory.Add(e.X, e.Y))
                {
                    SetPixel(x, y, SecondaryColor);
                }
            }
        }

        public override void OnMouseUp(object sender, MouseCaptureArgs e)
        {
            if(ActiveTool == PixelTool.Pencil && IsDrawing && _activePencilHistory?.ModifiedPoints.Count > 0)
            {
                IsDrawing = false;
                History.Add(_activePencilHistory);
                _activePencilHistory = null;
            }
        }

        public Task HandleAsync(EditArrangerPixelsEvent message, CancellationToken cancellationToken)
        {
            if(IsModified && HasArranger)
            {
                var result = _promptService.PromptUser($"{_arranger.Name} has been modified and will be closed. Would you like to save the changes?",
                    "Save changes", UserPromptChoices.YesNoCancel);

                if (result == UserPromptResult.Cancel)
                    return Task.CompletedTask;
                else if(result == UserPromptResult.Yes)
                {
                    // Save pixel data to arranger
                }
            }

            _arranger = message.ArrangerTransferModel.Arranger;
            _cropX = message.ArrangerTransferModel.X;
            _cropY = message.ArrangerTransferModel.Y;
            _cropWidth = message.ArrangerTransferModel.Width;
            _cropHeight = message.ArrangerTransferModel.Height;

            CreateImage();

            return Task.CompletedTask;
        }
    }
}
