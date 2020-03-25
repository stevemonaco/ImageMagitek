using Stylet;
using ImageMagitek.Colors;
using System.Threading.Tasks;
using TileShop.Shared.EventModels;
using TileShop.WPF.Models;
using System;

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

        private int _selectedIndex;
        public int SelectedIndex
        {
            get => _selectedIndex;
            set
            {
                SetAndNotify(ref _selectedIndex, value);
                if(SelectedIndex >= 0)
                    EditingItem = new ValidatedColorModel(_palette.GetForeignColor(SelectedIndex), SelectedIndex);
            }
        }

        private ValidatedColorModel _selectedItem;
        public ValidatedColorModel SelectedItem
        {
            get => _selectedItem;
            set => SetAndNotify(ref _selectedItem, value);
        }

        private ValidatedColorModel _editingItem;
        public ValidatedColorModel EditingItem
        {
            get => _editingItem;
            set => SetAndNotify(ref _editingItem, value);
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

            SelectedIndex = 0;
        }

        public Task SaveColor()
        {
            _palette.SetForeignColor(SelectedIndex, EditingItem.WorkingColor);
            IsModified = true;

            return Task.CompletedTask;
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
