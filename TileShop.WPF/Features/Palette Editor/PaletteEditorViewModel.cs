using Stylet;
using System.Threading.Tasks;
using TileShop.Shared.EventModels;
using TileShop.WPF.Models;
using ImageMagitek.Colors;
using System.Linq;
using ImageMagitek.Services;

namespace TileShop.WPF.ViewModels
{
    public class PaletteEditorViewModel : ResourceEditorBaseViewModel
    {
        private readonly Palette _palette;
        private readonly IPaletteService _paletteService;
        private readonly IEventAggregator _events;

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

        public PaletteEditorViewModel(Palette palette, IPaletteService paletteService, IEventAggregator events)
        {
            Resource = palette;
            _palette = palette;
            _paletteService = paletteService;
            _events = events;
            events.Subscribe(this);

            DisplayName = Resource?.Name ?? "Unnamed Palette";

            for(int i = 0; i < _palette.Entries; i++)
                Colors.Add(new ValidatedColorModel((IColor32)_palette.GetForeignColor(i), i, _paletteService.ColorFactory));

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

            var changeEvent = new PaletteChangedEvent(_palette);
            _events.PublishOnUIThread(changeEvent);
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

        public override void Undo()
        {
            throw new System.NotImplementedException();
        }

        public override void Redo()
        {
            throw new System.NotImplementedException();
        }

        public override void ApplyHistoryAction(HistoryAction action)
        {
            throw new System.NotImplementedException();
        }
    }
}
