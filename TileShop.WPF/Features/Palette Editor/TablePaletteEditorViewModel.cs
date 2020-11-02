using System;
using Stylet;
using ImageMagitek.Colors;
using ImageMagitek.Services;
using TileShop.Shared.EventModels;
using TileShop.WPF.Models;
using GongSolutions.Wpf.DragDrop;
using System.Linq;

namespace TileShop.WPF.ViewModels
{
    class TablePaletteEditorViewModel : ResourceEditorBaseViewModel, IDropTarget
    {
        private readonly Palette _palette;
        private readonly IPaletteService _paletteService;
        private readonly IColorFactory _colorFactory;
        private readonly IEventAggregator _events;

        private BindableCollection<ValidatedTableColorModel> _workingColors = new BindableCollection<ValidatedTableColorModel>();
        public BindableCollection<ValidatedTableColorModel> WorkingColors
        {
            get => _workingColors;
            set => SetAndNotify(ref _workingColors, value);
        }

        private BindableCollection<ValidatedTableColorModel> _availableColors = new BindableCollection<ValidatedTableColorModel>();
        public BindableCollection<ValidatedTableColorModel> AvailableColors
        {
            get => _availableColors;
            set => SetAndNotify(ref _availableColors, value);
        }

        public TablePaletteEditorViewModel(Palette palette, IPaletteService paletteService, IEventAggregator events)
        {
            Resource = palette;
            _palette = palette;
            _paletteService = paletteService;
            _colorFactory = _paletteService.ColorFactory;
            _events = events;
            events.Subscribe(this);

            DisplayName = Resource?.Name ?? "Unnamed Palette";

            for (int i = 0; i < _palette.Entries; i++)
            {
                WorkingColors.Add(new ValidatedTableColorModel((ITableColor)_palette.GetForeignColor(i), i, _paletteService.ColorFactory));
            }

            for (int i = 0; i < 64; i++)
            {
                var nesColor = new ValidatedTableColorModel(new ColorNes((uint)i), i, _colorFactory);
                AvailableColors.Add(nesColor);
            }
        }

        public override void SaveChanges()
        {
            for (int i = 0; i < WorkingColors.Count; i++)
            {
                var color = _colorFactory.CreateColor(_palette.ColorModel, WorkingColors[i].WorkingColor.Color);
                _palette.SetForeignColor(i, color);
            }

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

        public void MouseOver(ValidatedTableColorModel model)
        {
            string notifyMessage = $"Palette Index: {model.Index}";
            var notifyEvent = new NotifyStatusEvent(notifyMessage, NotifyStatusDuration.Indefinite);
            _events.PublishOnUIThread(notifyEvent);
        }

        public override void Undo()
        {
            throw new NotImplementedException();
        }

        public override void Redo()
        {
            throw new NotImplementedException();
        }

        public override void ApplyHistoryAction(HistoryAction action)
        {
            throw new NotImplementedException();
        }

        public void DragOver(IDropInfo dropInfo)
        {
            if (dropInfo.Data is ValidatedTableColorModel model)
            {
                dropInfo.Effects = System.Windows.DragDropEffects.Copy;
                dropInfo.DropTargetAdorner = DropTargetAdorners.Highlight;
            }
        }

        public void Drop(IDropInfo dropInfo)
        {
            if (dropInfo.Data is ValidatedTableColorModel dropModel && dropInfo.TargetItem is ValidatedTableColorModel targetModel)
            {
                var index = targetModel.Index;
                var color = new ColorNes(dropModel.WorkingColor.Color);
                WorkingColors[index] = new ValidatedTableColorModel(color, index, _colorFactory);

                IsModified = Enumerable
                    .Range(0, WorkingColors.Count)
                    .Any(x => WorkingColors[x].WorkingColor.Color != _palette.GetForeignColor(x).Color);
            }
        }
    }
}
