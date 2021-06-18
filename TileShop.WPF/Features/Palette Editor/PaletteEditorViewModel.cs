using Stylet;
using TileShop.Shared.EventModels;
using TileShop.WPF.Models;
using ImageMagitek.Colors;
using System.Linq;
using ImageMagitek.Services;
using System.Collections.Generic;
using System;

namespace TileShop.WPF.ViewModels
{
    public class PaletteEditorViewModel : PaletteEditorBaseViewModel<EditableColorBaseViewModel>
    {
        private int _selectedColorIndex;
        public int SelectedColorIndex
        {
            get => _selectedColorIndex;
            set
            {
                if (SetAndNotify(ref _selectedColorIndex, value) && value >= 0 && value < Colors.Count)
                {
                    var color = _palette.GetForeignColor(value);
                    if (color is IColor32 color32)
                        ActiveColor = new Color32ViewModel(color32, value, _colorFactory);
                    else if (color is ITableColor tableColor)
                        ActiveColor = new TableColorViewModel(tableColor, value, _colorFactory);
                }
            }
        }

        private EditableColorBaseViewModel _activeColor;
        public EditableColorBaseViewModel ActiveColor
        {
            get => _activeColor;
            set => SetAndNotify(ref _activeColor, value);
        }

        public PaletteEditorViewModel(Palette palette, IPaletteService paletteService, IProjectService projectService, IEventAggregator events) :
            base(palette, paletteService, projectService, events)
        {
            ActiveColor = Colors.FirstOrDefault();
        }

        public override void SaveSources()
        {
            _palette.SetColorSources(CreateColorSources());
            SaveChanges();

            Colors = new(CreateColorModels());

            ActiveColor = Colors.FirstOrDefault();
            PaletteSource = _palette.DataFile.Name;
            Entries = CountSourceColors();
            SelectedColorIndex = 0;
        }

        public void SaveActiveColor()
        {
            // The order here is very important as replacing a Colors item invalidates SelectedItem to -1 and
            // assigning a SelectedColorIndex reloads a color from the palette
            _palette.SetForeignColor(ActiveColor.Index, ActiveColor.WorkingColor);


            var model = CreateColorModel(_palette.GetForeignColor(SelectedColorIndex), SelectedColorIndex);
            var currentIndex = SelectedColorIndex;
            Colors[SelectedColorIndex] = model;

            SelectedColorIndex = currentIndex;
            SaveChanges();
        }

        public override void SaveChanges()
        {
            _palette.ZeroIndexTransparent = ZeroIndexTransparent;

            var projectTree = _projectService.GetContainingProject(_palette);
            var paletteNode = projectTree.GetResourceNode(_palette);
            _projectService.SaveResource(projectTree, paletteNode, false);
            IsModified = false;

            var changeEvent = new PaletteChangedEvent(_palette);
            _events.PublishOnUIThread(changeEvent);
        }

        public override void DiscardChanges()
        {
            _palette.Reload();
            ZeroIndexTransparent = _palette.ZeroIndexTransparent;
            ColorSourceModels = new(CreateColorSourceModels(_palette));
            Entries = CountSourceColors();
            IsModified = false;
        }

        public void MouseOver(Color32ViewModel model)
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

        protected override IEnumerable<EditableColorBaseViewModel> CreateColorModels()
        {
            for (int i = 0; i < _palette.Entries; i++)
            {
                yield return CreateColorModel(_palette.GetForeignColor(i), i);
            }
        }

        protected EditableColorBaseViewModel CreateColorModel(IColor foreignColor, int index)
        {
            if (foreignColor is IColor32 color32)
                return new Color32ViewModel(color32, index, _colorFactory);
            else if (foreignColor is ITableColor tableColor)
                return new TableColorViewModel(tableColor, index, _colorFactory);
            else
                throw new NotSupportedException($"Color of type '{foreignColor.GetType()}' is not supported for editing");
        }
    }
}
