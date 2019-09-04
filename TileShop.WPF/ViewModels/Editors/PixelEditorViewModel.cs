using Caliburn.Micro;
using ImageMagitek;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TileShop.Shared.EventModels;
using TileShop.WPF.Behaviors;
using TileShop.WPF.Helpers;
using TileShop.WPF.Services;

namespace TileShop.WPF.ViewModels.Editors
{
    public class PixelEditorViewModel : ArrangerEditorViewModel, IMouseCaptureProxy, IHandle<EditArrangerPixelsEvent>
    {
        private IUserPromptService _promptService;
        private int _cropX;
        private int _cropY;
        private int _cropWidth;
        private int _cropHeight;

        public override string Name => "Pixel Editor";
        public event EventHandler Capture;
        public event EventHandler Release;

        private bool _hasArranger;
        public bool HasArranger
        {
            get => _hasArranger;
            set
            {
                _hasArranger = value;
                NotifyOfPropertyChange(() => HasArranger);
            }
        }

        public override bool CanShowGridlines => HasArranger;

        public PixelEditorViewModel(IEventAggregator events, IUserPromptService promptService)
        {
            _events = events;
            _promptService = promptService;
        }

        public void CreateImage()
        {
            using var arrangerImage = new ArrangerImage();
            arrangerImage.RenderSubImage(_arranger, _cropX, _cropY, _cropWidth, _cropHeight);
            ArrangerSource = new ImageRgba32Source(arrangerImage.Image);
        }

        public override void OnMouseDown(object sender, MouseCaptureArgs e)
        {
            throw new NotImplementedException();
        }

        public override void OnMouseLeave(object sender, MouseCaptureArgs e)
        {
            throw new NotImplementedException();
        }

        public override void OnMouseMove(object sender, MouseCaptureArgs e)
        {
            throw new NotImplementedException();
        }

        public override void OnMouseUp(object sender, MouseCaptureArgs e)
        {
            throw new NotImplementedException();
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
