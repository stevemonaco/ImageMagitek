using System;
using System.Windows.Media.Imaging;
using Stylet;
using ImageMagitek;
using ImageMagitek.Colors;
using ImageMagitek.Services;
using TileShop.WPF.Imaging;
using TileShop.WPF.Services;

namespace TileShop.WPF.ViewModels
{
    public class ImportImageViewModel : Screen
    {
        private readonly Arranger _arranger;
        private readonly IPaletteService _paletteService;
        private readonly IFileSelectService _fileSelect;

        private readonly IndexedImage _originalIndexed;
        private readonly DirectImage _originalDirect;

        private IndexedImage _importedIndexed;
        private DirectImage _importedDirect;

        private string _imageFileName;
        public string ImageFileName
        {
            get => _imageFileName;
            set => SetAndNotify(ref _imageFileName, value);
        }

        private BitmapSource _originalSource;
        public BitmapSource OriginalSource
        {
            get => _originalSource;
            set => SetAndNotify(ref _originalSource, value);
        }

        private BitmapSource _importedSource;
        public BitmapSource ImportedSource
        {
            get => _importedSource;
            set => SetAndNotify(ref _importedSource, value);
        }

        private bool _useExactMatching = true;
        public bool UseExactMatching
        {
            get => _useExactMatching;
            set
            {
                SetAndNotify(ref _useExactMatching, value);
                if (!string.IsNullOrEmpty(ImageFileName))
                    ImportIndexed(ImageFileName);
            }
        }

        private bool _canImport;
        public bool CanImport
        {
            get => _canImport;
            set => SetAndNotify(ref _canImport, value);
        }

        private string _importError;
        public string ImportError
        {
            get => _importError;
            set => SetAndNotify(ref _importError, value);
        }

        protected int _zoom = 1;
        public int Zoom
        {
            get => _zoom;
            set => SetAndNotify(ref _zoom, value);
        }

        public int MinZoom => 1;
        public int MaxZoom { get; protected set; } = 16;

        public bool IsTiledArranger => _arranger.Layout == ArrangerLayout.Tiled;
        public bool IsSingleArranger => _arranger.Layout == ArrangerLayout.Single;

        public ImportImageViewModel(Arranger arranger, IPaletteService paletteService, IFileSelectService fileSelect)
        {
            _arranger = arranger;
            _paletteService = paletteService;
            _fileSelect = fileSelect;

            if (_arranger.ColorType == PixelColorType.Indexed)
            {
                _originalIndexed = new IndexedImage(_arranger, _paletteService.DefaultPalette);
                OriginalSource = new IndexedImageSource(_originalIndexed, _arranger, _paletteService.DefaultPalette);
            }
            else if (_arranger.ColorType == PixelColorType.Direct)
            {
                _originalDirect = new DirectImage(_arranger);
                OriginalSource = new DirectImageSource(_originalDirect);
            }
        }

        protected override void OnViewLoaded()
        {
            base.OnViewLoaded();

            BrowseForImportFile();
        }

        public void BrowseForImportFile()
        {
            var fileName = _fileSelect.GetImportArrangerFileNameByUser();

            if (fileName is object)
            {
                if (_arranger.ColorType == PixelColorType.Indexed)
                {
                    ImportIndexed(fileName);
                }
                else if (_arranger.ColorType == PixelColorType.Direct)
                {
                    _importedDirect = new DirectImage(_arranger);
                    _importedDirect.ImportImage(ImageFileName, new ImageFileAdapter());
                    ImportedSource = new DirectImageSource(_importedDirect);
                    CanImport = true;
                }
            }
        }

        private void ImportIndexed(string fileName)
        {
            var matchStrategy = UseExactMatching ? ColorMatchStrategy.Exact : ColorMatchStrategy.Nearest;
            _importedIndexed = new IndexedImage(_arranger, _paletteService.DefaultPalette);

            var result = _importedIndexed.TryImportImage(fileName, new ImageFileAdapter(), matchStrategy);

            result.Switch(
                success =>
                {
                    ImageFileName = fileName;
                    ImportError = string.Empty;
                    CanImport = true;
                    ImportedSource = new IndexedImageSource(_importedIndexed, _arranger, _paletteService.DefaultPalette);
                },
                fail =>
                {
                    CanImport = false;
                    ImageFileName = fileName;
                    ImportedSource = null;
                    ImportError = fail.Reason;
                });
        }

        public void ZoomIn() => Zoom = Math.Clamp(Zoom + 1, MinZoom, MaxZoom);
        public void ZoomOut() => Zoom = Math.Clamp(Zoom - 1, MinZoom, MaxZoom);

        public void ConfirmImport()
        {
            if (_arranger.ColorType == PixelColorType.Indexed)
                _importedIndexed.SaveImage();
            else if (_arranger.ColorType == PixelColorType.Direct)
                _importedDirect.SaveImage();

            RequestClose(true);
        }

        public void Cancel() => RequestClose(false);
    }
}
