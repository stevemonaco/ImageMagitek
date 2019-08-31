using Caliburn.Micro;
using ImageMagitek;
using System;
using System.Collections.Generic;
using System.Text;
using TileShop.WPF.Behaviors;
using TileShop.WPF.Helpers;

namespace TileShop.WPF.ViewModels
{
    public class SequentialArrangerEditorViewModel : EditorBaseViewModel, IMouseCaptureProxy
    {
        private Arranger _arranger;
        private ArrangerImage _arrangerImage = new ArrangerImage();
        private IEventAggregator _events;

        private ImageRgba32Source _arrangerSource;

        public event EventHandler Capture;
        public event EventHandler Release;

        public ImageRgba32Source ArrangerSource
        {
            get => _arrangerSource;
            set
            {
                _arrangerSource = value;
                NotifyOfPropertyChange(() => ArrangerSource);
            }
        }

        public SequentialArrangerEditorViewModel(Arranger arranger, IEventAggregator events)
        {
            Resource = arranger;
            _arranger = arranger;
            _events = events;

            _arrangerImage.Render(_arranger);
            ArrangerSource = new ImageRgba32Source(_arrangerImage.Image);
        }

        public void OnMouseDown(object sender, MouseCaptureArgs e)
        {
            throw new NotImplementedException();
        }

        public void OnMouseLeave(object sender, MouseCaptureArgs e)
        {
            throw new NotImplementedException();
        }

        public void OnMouseMove(object sender, MouseCaptureArgs e)
        {
            throw new NotImplementedException();
        }

        public void OnMouseUp(object sender, MouseCaptureArgs e)
        {
            throw new NotImplementedException();
        }
    }
}
