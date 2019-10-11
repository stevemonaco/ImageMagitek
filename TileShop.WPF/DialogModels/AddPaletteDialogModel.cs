using Caliburn.Micro;
using ImageMagitek;
using ImageMagitek.Colors;

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

        private BindableCollection<DataFile> _dataFiles = new BindableCollection<DataFile>();
        public BindableCollection<DataFile> DataFiles
        {
            get => _dataFiles;
            set => Set(ref _dataFiles, value);
        }

        private DataFile _selectedDataFile;
        public DataFile SelectedDataFile
        {
            get => _selectedDataFile;
            set => Set(ref _selectedDataFile, value);
        }

        private BindableCollection<string> _colorModels = new BindableCollection<string>();
        public BindableCollection<string> ColorModels
        {
            get => _colorModels;
            set => Set(ref _colorModels, value);
        }

        private string _selectedColorModel;
        public string SelectedColorModel
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

        private bool _zeroIndexTransparent = true;
        public bool ZeroIndexTransparent
        {
            get => _zeroIndexTransparent;
            set => Set(ref _zeroIndexTransparent, value);
        }

        private int _fileOffset;
        public int FileOffset
        {
            get => _fileOffset;
            set => Set(ref _fileOffset, value);
        }

        public BindableCollection<string> _validationErrors = new BindableCollection<string>();
        private BindableCollection<string> ValidationErrors;
        public BindableCollection<string> MyProperty
        {
            get => ValidationErrors;
            set => Set(ref ValidationErrors, value);
        }

        private bool? _dialogResult;
        public bool? DialogResult
        {
            get => _dialogResult;
            set => Set(ref _dialogResult, value);
        }

        public void Add() => DialogResult = true;

        public void Cancel() => DialogResult = false;
    }
}
