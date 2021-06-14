using Stylet;
using System.Threading.Tasks;
using TileShop.Shared.EventModels;
using TileShop.WPF.Models;
using ImageMagitek.Colors;
using System.Linq;
using ImageMagitek.Services;
using System.Collections.Generic;
using ImageMagitek;
using ImageMagitek.Utility.Parsing;
using System;

namespace TileShop.WPF.ViewModels
{
    public class PaletteEditorViewModel : ResourceEditorBaseViewModel
    {
        private readonly Palette _palette;
        private readonly IPaletteService _paletteService;
        private readonly IColorFactory _colorFactory;
        private readonly IProjectService _projectService;
        private readonly IEventAggregator _events;

        private BindableCollection<ColorSourceModel> _colorSourceModels = new();
        public BindableCollection<ColorSourceModel> ColorSourceModels
        {
            get => _colorSourceModels;
            set => SetAndNotify(ref _colorSourceModels, value);
        }

        private BindableCollection<ValidatedColor32Model> _colors = new();
        public BindableCollection<ValidatedColor32Model> Colors
        {
            get => _colors;
            set => SetAndNotify(ref _colors, value);
        }

        private int _selectedColorIndex;
        public int SelectedColorIndex
        {
            get => _selectedColorIndex;
            set
            {
                if (SetAndNotify(ref _selectedColorIndex, value) && value >= 0 && value < Colors.Count)
                {
                    ActiveColor = new ValidatedColor32Model((IColor32)_palette.GetForeignColor(value), value, _colorFactory);
                }
            }
        }

        private ValidatedColor32Model _activeColor;
        public ValidatedColor32Model ActiveColor
        {
            get => _activeColor;
            set => SetAndNotify(ref _activeColor, value);
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

            Colors.AddRange(CreateColorModels());
            ColorSourceModels.AddRange(CreateColorSourceModels(_palette));

            ActiveColor = Colors.FirstOrDefault();
            PaletteSource = _palette.DataFile.Name;
            Entries = CountSourceColors();
            SelectedColorIndex = 0;
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
            var model = new ValidatedColor32Model((IColor32)_palette.GetForeignColor(SelectedColorIndex), SelectedColorIndex, _colorFactory);
            var currentIndex = SelectedColorIndex;
            Colors[SelectedColorIndex] = model;

            SelectedColorIndex = currentIndex;
            SaveChanges();
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

        public void MouseOver(ValidatedColor32Model model)
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

        private IEnumerable<ValidatedColor32Model> CreateColorModels()
        {
            for (int i = 0; i < _palette.Entries; i++)
                yield return new ValidatedColor32Model((IColor32)_palette.GetForeignColor(i), i, _colorFactory);
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
                    yield return new ProjectNativeColorSource((ColorRgba32) nativeColor);
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
