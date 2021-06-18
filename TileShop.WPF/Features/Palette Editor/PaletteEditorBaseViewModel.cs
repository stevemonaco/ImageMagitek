using ImageMagitek;
using ImageMagitek.Colors;
using ImageMagitek.Services;
using ImageMagitek.Utility.Parsing;
using Stylet;
using System;
using System.Collections.Generic;
using System.Linq;
using TileShop.WPF.Models;

namespace TileShop.WPF.ViewModels
{
    public abstract class PaletteEditorBaseViewModel<T> : ResourceEditorBaseViewModel
    {
        protected readonly Palette _palette;
        protected readonly IPaletteService _paletteService;
        protected readonly IColorFactory _colorFactory;
        protected readonly IProjectService _projectService;
        protected readonly IEventAggregator _events;

        private BindableCollection<T> _colors = new();
        public BindableCollection<T> Colors
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

        public PaletteEditorBaseViewModel(Palette palette, IPaletteService paletteService, IProjectService projectService, IEventAggregator events)
        {
            Resource = palette;
            _palette = palette;
            _paletteService = paletteService;
            _colorFactory = _paletteService.ColorFactory;
            _projectService = projectService;
            _events = events;
            events.Subscribe(this);

            DisplayName = Resource?.Name ?? "Unnamed Palette";

            ColorModel = _palette.ColorModel;
            Colors.AddRange(CreateColorModels());
            ColorSourceModels.AddRange(CreateColorSourceModels(_palette));

            PaletteSource = _palette.DataFile.Name;
            Entries = CountSourceColors();
        }

        protected abstract IEnumerable<T> CreateColorModels();

        public abstract void SaveSources();

        public virtual void AddNewFileColorSource()
        {
            ColorSourceModels.Add(new FileColorSourceModel(0, 0));
            Entries = CountSourceColors();
        }

        public virtual void AddNewNativeColorSource()
        {
            var color = _colorFactory.CreateColor(ColorModel.Rgba32, 0, 0, 0, 255);
            var hexString = _colorFactory.ToHexString(color);
            ColorSourceModels.Add(new NativeColorSourceModel(hexString));
            Entries = CountSourceColors();
        }

        public virtual void AddNewForeignColorSource()
        {
            var color = _colorFactory.CreateColor(_palette.ColorModel, 0);
            var hexString = _colorFactory.ToHexString(color);
            ColorSourceModels.Add(new ForeignColorSourceModel(hexString));
            Entries = CountSourceColors();
        }

        public virtual void RemoveColorSource(ColorSourceModel model)
        {
            ColorSourceModels.Remove(model);
            Entries = CountSourceColors();
        }

        protected virtual IEnumerable<ColorSourceModel> CreateColorSourceModels(Palette pal)
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

        protected virtual int CountSourceColors()
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

        protected virtual IEnumerable<IColorSource> CreateColorSources()
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
