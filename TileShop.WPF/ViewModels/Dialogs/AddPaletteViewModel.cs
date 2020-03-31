using Stylet;
using ImageMagitek;
using System.Collections.Generic;

namespace TileShop.WPF.ViewModels
{
    public class AddPaletteViewModel : Screen
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

        private BindableCollection<string> _existingResourceNames = new BindableCollection<string>();
        public BindableCollection<string> ExistingResourceNames
        {
            get => _existingResourceNames;
            set => SetAndNotify(ref _existingResourceNames, value);
        }

        private BindableCollection<string> _validationErrors = new BindableCollection<string>();
        public BindableCollection<string> ValidationErrors
        {
            get => _validationErrors;
            set => SetAndNotify(ref _validationErrors, value);
        }

        private bool _canAdd;
        public bool CanAdd
        {
            get => _canAdd;
            set => SetAndNotify(ref _canAdd, value);
        }

        public AddPaletteViewModel() { }
        
        public AddPaletteViewModel(IEnumerable<string> existingResourceNames)
        {
            ExistingResourceNames.AddRange(existingResourceNames);
        }

        public void Add() => RequestClose(true);

        public void Cancel() => RequestClose(false);

        public void ValidateModel()
        {
            ValidationErrors.Clear();

            if (string.IsNullOrWhiteSpace(PaletteName))
                ValidationErrors.Add($"Name is invalid");

            if (ExistingResourceNames.Contains(PaletteName))
                ValidationErrors.Add($"Name already exists");

            CanAdd = ValidationErrors.Count == 0;
        }
    }
}
