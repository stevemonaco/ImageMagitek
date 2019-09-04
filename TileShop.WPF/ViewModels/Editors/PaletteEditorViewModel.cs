using Caliburn.Micro;
using ImageMagitek.Colors;
using ImageMagitek.Project;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using TileShop.Shared.EventModels;
using TileShop.WPF.Models;

namespace TileShop.WPF.ViewModels
{
    public class PaletteEditorViewModel : ResourceEditorBaseViewModel
    {
        private Palette _palette;
        private IEventAggregator _events;

        private BindableCollection<PaletteItemModel> _colors = new BindableCollection<PaletteItemModel>();
        public BindableCollection<PaletteItemModel> Colors
        {
            get => _colors;
            set => Set(ref _colors, value);
        }

        private int _selectedIndex;
        public int SelectedIndex
        {
            get => _selectedIndex;
            set
            {
                Set(ref _selectedIndex, value);
                if(SelectedIndex >= 0)
                    EditingItem = new PaletteItemModel(_palette.GetForeignColor(SelectedIndex), SelectedIndex);
            }
        }

        private PaletteItemModel _selectedItem;
        public PaletteItemModel SelectedItem
        {
            get => _selectedItem;
            set => Set(ref _selectedItem, value);
        }

        private PaletteItemModel _editingItem;
        public PaletteItemModel EditingItem
        {
            get => _editingItem;
            set => Set(ref _editingItem, value);
        }

        public override string DisplayName => Resource?.Name;

        public PaletteEditorViewModel(Palette palette, IEventAggregator events)
        {
            Resource = palette;
            _palette = palette;
            _events = events;

            for(int i = 0; i < _palette.Entries; i++)
                Colors.Add(new PaletteItemModel(_palette.GetForeignColor(i), i));

            SelectedIndex = 0;
        }

        public Task SaveColor()
        {
            _palette.SetForeignColor(SelectedIndex, EditingItem.WorkingColor);
            IsModified = true;

            return Task.CompletedTask;
        }

        public void MouseOver(PaletteItemModel model)
        {
            string notifyMessage = $"Palette Index: {model.Index}";
            var notifyEvent = new NotifyStatusEvent(notifyMessage, NotifyStatusDuration.Indefinite);
            _events.PublishOnUIThreadAsync(notifyEvent);
        }
    }
}
