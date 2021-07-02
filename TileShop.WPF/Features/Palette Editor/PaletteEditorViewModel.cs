using System;
using System.Collections.Generic;
using System.Linq;
using Stylet;
using TileShop.Shared.EventModels;
using TileShop.WPF.Models;
using ImageMagitek.Colors;
using ImageMagitek.Services;
using ImageMagitek.Utility.Parsing;
using ImageMagitek;

namespace TileShop.WPF.ViewModels
{
    public class PaletteEditorViewModel : ResourceEditorBaseViewModel
    {
        protected readonly Palette _palette;
        protected readonly IPaletteService _paletteService;
        protected readonly IColorFactory _colorFactory;
        protected readonly IProjectService _projectService;
        protected readonly IEventAggregator _events;

        private BindableCollection<EditableColorBaseViewModel> _colors = new();
        public BindableCollection<EditableColorBaseViewModel> Colors
        {
            get => _colors;
            set => SetAndNotify(ref _colors, value);
        }

        private BindableCollection<ColorSourceModel> _colorSourceModels = new();
        public BindableCollection<ColorSourceModel> ColorSourceModels
        {
            get => _colorSourceModels;
            set => SetAndNotify(ref _colorSourceModels, value);
        }

        private string _paletteSource;
        public string PaletteSource
        {
            get => _paletteSource;
            set => SetAndNotify(ref _paletteSource, value);
        }

        private int _entries;
        public int Entries
        {
            get => _entries;
            set => SetAndNotify(ref _entries, value);
        }

        private bool _zeroIndexTransparent;
        public bool ZeroIndexTransparent
        {
            get => _zeroIndexTransparent;
            set
            {
                if (SetAndNotify(ref _zeroIndexTransparent, value))
                    IsModified = true;
            }
        }

        private ColorModel _colorModel;
        public ColorModel ColorModel
        {
            get => _colorModel;
            set => SetAndNotify(ref _colorModel, value);
        }

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

        public PaletteEditorViewModel(Palette palette, IPaletteService paletteService, IProjectService projectService, IEventAggregator events)
        {
            Resource = palette;
            _palette = palette;
            _paletteService = paletteService;
            _colorFactory = _paletteService.ColorFactory;
            _projectService = projectService;
            _events = events;
            events.Subscribe(this);

            DisplayName = Resource?.Name ?? "Unnamed Palette";

            _zeroIndexTransparent = _palette.ZeroIndexTransparent;
            ColorModel = _palette.ColorModel;
            Colors.AddRange(CreateColorModels());
            ColorSourceModels.AddRange(CreateColorSourceModels(_palette));

            PaletteSource = _palette.DataFile.Name;
            Entries = CountSourceColors();

            if (Entries > 0)
            {
                var color = _palette.GetForeignColor(0);
                if (color is IColor32 color32)
                    ActiveColor = new Color32ViewModel(color32, 0, _colorFactory);
                else if (color is ITableColor tableColor)
                    ActiveColor = new TableColorViewModel(tableColor, 0, _colorFactory);
            }
        }

        public void SaveSources()
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

        private IEnumerable<EditableColorBaseViewModel> CreateColorModels()
        {
            for (int i = 0; i < _palette.Entries; i++)
            {
                yield return CreateColorModel(_palette.GetForeignColor(i), i);
            }
        }

        private EditableColorBaseViewModel CreateColorModel(IColor foreignColor, int index)
        {
            if (foreignColor is IColor32 color32)
                return new Color32ViewModel(color32, index, _colorFactory);
            else if (foreignColor is ITableColor tableColor)
                return new TableColorViewModel(tableColor, index, _colorFactory);
            else
                throw new NotSupportedException($"Color of type '{foreignColor.GetType()}' is not supported for editing");
        }

        public void AddNewFileColorSource()
        {
            ColorSourceModels.Add(new FileColorSourceModel(0, 0));
            Entries = CountSourceColors();
        }

        public void AddNewNativeColorSource()
        {
            var color = _colorFactory.CreateColor(ColorModel.Rgba32, 0, 0, 0, 255);
            var hexString = _colorFactory.ToHexString(color);
            ColorSourceModels.Add(new NativeColorSourceModel(hexString));
            Entries = CountSourceColors();
        }

        public void AddNewForeignColorSource()
        {
            var color = _colorFactory.CreateColor(_palette.ColorModel, 0);
            var hexString = _colorFactory.ToHexString(color);
            ColorSourceModels.Add(new ForeignColorSourceModel(hexString));
            Entries = CountSourceColors();
        }

        public void RemoveColorSource(ColorSourceModel model)
        {
            ColorSourceModels.Remove(model);
            Entries = CountSourceColors();
        }

        private IEnumerable<ColorSourceModel> CreateColorSourceModels(Palette pal)
        {
            var size = _colorFactory.CreateColor(pal.ColorModel).Size;

            int i = 0;
            while (i < pal.ColorSources.Length)
            {
                if (pal.ColorSources[i] is FileColorSource fileSource)
                {
                    var sources = pal.ColorSources.Skip(i)
                        .TakeWhile((x, i) => x is FileColorSource && (x as FileColorSource).Offset == (fileSource.Offset + i * size))
                        .ToList();

                    var fileSourceModel = new FileColorSourceModel(fileSource.Offset.FileOffset, sources.Count);
                    yield return fileSourceModel;

                    i += sources.Count;
                }
                else if (pal.ColorSources[i] is ProjectNativeColorSource nativeSource)
                {
                    var hexString = _colorFactory.ToHexString(nativeSource.Value);
                    var nativeSourceModel = new NativeColorSourceModel(hexString);
                    yield return nativeSourceModel;
                    i++;
                }
                else if (pal.ColorSources[i] is ProjectForeignColorSource foreignSource)
                {
                    var hexString = _colorFactory.ToHexString(foreignSource.Value);
                    var foreignSourceModel = new ForeignColorSourceModel(hexString);
                    yield return foreignSourceModel;
                    i++;
                }
                else if (pal.ColorSources[i] is ScatteredColorSource scatteredSource)
                {
                }
            }
        }

        private int CountSourceColors()
        {
            int count = 0;

            foreach (var source in ColorSourceModels)
            {
                count += source switch
                {
                    FileColorSourceModel fileSource => fileSource.Entries,
                    NativeColorSourceModel => 1,
                    ForeignColorSourceModel => 1,
                    _ => 0
                };
            }

            return count;
        }

        private IEnumerable<IColorSource> CreateColorSources()
        {
            var size = _colorFactory.CreateColor(_palette.ColorModel).Size;

            for (int i = 0; i < ColorSourceModels.Count; i++)
            {
                var sourceModel = ColorSourceModels[i];
                if (sourceModel is FileColorSourceModel fileModel)
                {
                    var offset = new FileBitAddress(fileModel.FileAddress, 0);
                    for (int j = 0; j < fileModel.Entries; j++)
                        yield return new FileColorSource(offset + j * size);
                }
                else if (sourceModel is NativeColorSourceModel nativeModel)
                {
                    ColorParser.TryParse(nativeModel.NativeHexColor, ColorModel.Rgba32, out var nativeColor);
                    yield return new ProjectNativeColorSource((ColorRgba32)nativeColor);
                }
                else if (sourceModel is ForeignColorSourceModel foreignModel)
                {
                    ColorParser.TryParse(foreignModel.ForeignHexColor, _palette.ColorModel, out var foreignColor);
                    yield return new ProjectForeignColorSource(foreignColor);
                }
                else if (sourceModel is ScatteredColorSourceModel scatteredModel)
                {
                    throw new NotSupportedException();
                }
            }
        }
    }
}
