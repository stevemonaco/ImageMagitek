using Caliburn.Micro;
using ImageMagitek;
using ImageMagitek.Colors;
using System;
using System.Collections.Generic;
using System.Text;

namespace TileShop.WPF.DialogModels
{
    public class AddPaletteDialogModel : PropertyChangedBase
    {
        private string _paletteName;
        public string PaletteName
        {
            get => _paletteName;
            set => Set(ref _paletteName, value);
        }

        private BindableCollection<DataFile> _dataFiles;
        public BindableCollection<DataFile> DataFile
        {
            get => _dataFiles;
            set => Set(ref _dataFiles, value);
        }

        private string _selectedDataFile;
        public string SelectedDataFile
        {
            get => _selectedDataFile;
            set => Set(ref _selectedDataFile, value);
        }

        private BindableCollection<ColorModel> _colorModels;
        public BindableCollection<ColorModel> ColorModels
        {
            get => _colorModels;
            set => Set(ref _colorModels, value);
        }

        private ColorModel _selectedColorModel;
        public ColorModel SelectedColorModel
        {
            get => _selectedColorModel;
            set => Set(ref _selectedColorModel, value);
        }

        private int _entries;
        public int Entries
        {
            get => _entries;
            set => Set(ref _entries, value);
        }

        private int _fileOffset;
        public int FileOffset
        {
            get => _fileOffset;
            set => Set(ref _fileOffset, value);
        }
    }
}
