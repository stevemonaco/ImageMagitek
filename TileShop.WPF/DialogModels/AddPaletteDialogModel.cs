using Stylet;
using ImageMagitek;

namespace TileShop.WPF.DialogModels
{
    public class AddPaletteDialogModel : PropertyChangedBase
    {
        private string _paletteName;
        public string PaletteName
        {
            get => _paletteName;
            set => SetAndNotify(ref _paletteName, value);
        }

        private BindableCollection<DataFile> _dataFiles = new BindableCollection<DataFile>();
        public BindableCollection<DataFile> DataFiles
        {
            get => _dataFiles;
            set => SetAndNotify(ref _dataFiles, value);
        }

        private DataFile _selectedDataFile;
        public DataFile SelectedDataFile
        {
            get => _selectedDataFile;
            set => SetAndNotify(ref _selectedDataFile, value);
        }

        private BindableCollection<string> _colorModels = new BindableCollection<string>();
        public BindableCollection<string> ColorModels
        {
            get => _colorModels;
            set => SetAndNotify(ref _colorModels, value);
        }

        private string _selectedColorModel;
        public string SelectedColorModel
        {
            get => _selectedColorModel;
            set => SetAndNotify(ref _selectedColorModel, value);
        }

        private int _entries;
        public int Entries
        {
            get => _entries;
            set => SetAndNotify(ref _entries, value);
        }

        private bool _zeroIndexTransparent = true;
        public bool ZeroIndexTransparent
        {
            get => _zeroIndexTransparent;
            set => SetAndNotify(ref _zeroIndexTransparent, value);
        }

        private int _fileOffset;
        public int FileOffset
        {
            get => _fileOffset;
            set => SetAndNotify(ref _fileOffset, value);
        }

        public BindableCollection<string> _validationErrors = new BindableCollection<string>();
        private BindableCollection<string> ValidationErrors;
        public BindableCollection<string> MyProperty
        {
            get => ValidationErrors;
            set => SetAndNotify(ref ValidationErrors, value);
        }

        private bool? _dialogResult;
        public bool? DialogResult
        {
            get => _dialogResult;
            set => SetAndNotify(ref _dialogResult, value);
        }

        public void Add() => DialogResult = true;

        public void Cancel() => DialogResult = false;
    }
}
