using ImageMagitek;
using ImageMagitek.Colors;
using TileShop.Shared.Services;
using TileShop.AvaloniaUI.ViewExtenders;
using CommunityToolkit.Mvvm.ComponentModel;
using TileShop.AvaloniaUI.Imaging;
using CommunityToolkit.Mvvm.Input;

namespace TileShop.AvaloniaUI.ViewModels;

public partial class ImportImageViewModel : DialogViewModel<ImportImageViewModel>
{
    private readonly Arranger _arranger;
    private readonly IFileSelectService _fileSelect;

    private readonly IndexedImage _originalIndexed;
    private readonly DirectImage _originalDirect;

    private IndexedImage _importedIndexed;
    private DirectImage _importedDirect;

    [ObservableProperty] private string _imageFileName;
    [ObservableProperty] private BitmapAdapter _originalSource;
    [ObservableProperty] private BitmapAdapter _importedSource;
    [ObservableProperty] private bool _isIndexedImage;

    private bool _useExactMatching = true;
    public bool UseExactMatching
    {
        get => _useExactMatching;
        set
        {
            SetProperty(ref _useExactMatching, value);
            if (!string.IsNullOrEmpty(ImageFileName))
            {
                if (_arranger.ColorType == PixelColorType.Indexed)
                    ImportIndexed(ImageFileName);
                else if (_arranger.ColorType == PixelColorType.Direct)
                    ImportDirect(ImageFileName);
            }
        }
    }

    [ObservableProperty] private bool _canImport;
    [ObservableProperty] private string _importError;
    [ObservableProperty] protected int _zoom = 1;

    public bool IsTiledArranger => _arranger.Layout == ElementLayout.Tiled;
    public bool IsSingleArranger => _arranger.Layout == ElementLayout.Single;

    public ImportImageViewModel(Arranger arranger, IFileSelectService fileSelect)
    {
        _arranger = arranger;
        _fileSelect = fileSelect;

        if (_arranger.ColorType == PixelColorType.Indexed)
        {
            _originalIndexed = new IndexedImage(_arranger);
            OriginalSource = new IndexedBitmapAdapter(_originalIndexed);
        }
        else if (_arranger.ColorType == PixelColorType.Direct)
        {
            _originalDirect = new DirectImage(_arranger);
            OriginalSource = new DirectBitmapAdapter(_originalDirect);
        }

        Title = "Import Image Into Arranger";
    }

    //protected override void OnViewLoaded()
    //{
    //    base.OnViewLoaded();

    //    BrowseForImportFile();
    //}

    [RelayCommand]
    public void BrowseForImportFile()
    {
        var fileName = _fileSelect.GetImportArrangerFileNameByUser();

        if (fileName is null)
            return;

        ImageFileName = fileName;

        if (_arranger.ColorType == PixelColorType.Indexed)
        {
            ImportIndexed(ImageFileName);
        }
        else if (_arranger.ColorType == PixelColorType.Direct)
        {
            ImportDirect(ImageFileName);
        }
    }

    private void ImportIndexed(string fileName)
    {
        var matchStrategy = UseExactMatching ? ColorMatchStrategy.Exact : ColorMatchStrategy.Nearest;
        _importedIndexed = new IndexedImage(_arranger);

        var result = _importedIndexed.TryImportImage(fileName, new ImageSharpFileAdapter(), matchStrategy);

        result.Switch(
            success =>
            {
                ImageFileName = fileName;
                ImportError = string.Empty;
                CanImport = true;
                ImportedSource = new IndexedBitmapAdapter(_importedIndexed);
            },
            fail =>
            {
                CanImport = false;
                ImageFileName = fileName;
                ImportedSource = null;
                ImportError = fail.Reason;
            });
    }

    private void ImportDirect(string fileName)
    {
        _importedDirect = new DirectImage(_arranger);
        _importedDirect.ImportImage(ImageFileName, new ImageSharpFileAdapter());
        ImportedSource = new DirectBitmapAdapter(_importedDirect);
        CanImport = true;
    }

    public override void Ok(ImportImageViewModel? result)
    {
        if (_arranger.ColorType == PixelColorType.Indexed)
            _importedIndexed.SaveImage();
        else if (_arranger.ColorType == PixelColorType.Direct)
            _importedDirect.SaveImage();

        base.Ok(result);
    }
}
