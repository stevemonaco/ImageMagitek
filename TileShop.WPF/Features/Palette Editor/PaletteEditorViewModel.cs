using Stylet;
using System.Threading.Tasks;
using TileShop.Shared.EventModels;
using TileShop.WPF.Models;
using ImageMagitek.Colors;
using System.Linq;

namespace TileShop.WPF.ViewModels
{
    public class PaletteEditorViewModel : ResourceEditorBaseViewModel
    {
        private Palette _palette;
        private IEventAggregator _events;

        private BindableCollection<ValidatedColorModel> _colors = new BindableCollection<ValidatedColorModel>();
        public BindableCollection<ValidatedColorModel> Colors
        {
            get => _colors;
            set => SetAndNotify(ref _colors, value);
        }

        private ValidatedColorModel _selectedColor;
        public ValidatedColorModel SelectedColor
        {
            get => _selectedColor;
            set => SetAndNotify(ref _selectedColor, value);
        }

        public PaletteEditorViewModel(Palette palette, IEventAggregator events)
        {
            Resource = palette;
            _palette = palette;
            _events = events;
            events.Subscribe(this);

            DisplayName = Resource?.Name ?? "Unnamed Palette";

            for(int i = 0; i < _palette.Entries; i++)
                Colors.Add(new ValidatedColorModel(_palette.GetForeignColor(i), i));

            SelectedColor = Colors.First();
        }

        public void SaveColor()
        {
            SelectedColor.SaveColor();
            _palette.SetForeignColor(SelectedColor.Index, SelectedColor.WorkingColor);
            IsModified = true;
        }

        public override void SaveChanges()
        {
            _palette.SavePalette();
            IsModified = false;
        }

        public override void DiscardChanges()
        {
            _palette.Reload();
            IsModified = false;
        }

        public void MouseOver(ValidatedColorModel model)
        {
            string notifyMessage = $"Palette Index: {model.Index}";
            var notifyEvent = new NotifyStatusEvent(notifyMessage, NotifyStatusDuration.Indefinite);
            _events.PublishOnUIThread(notifyEvent);
        }
    }
}
