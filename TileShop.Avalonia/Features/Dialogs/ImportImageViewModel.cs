using ImageMagitek;
using ImageMagitek.Colors;
using TileShop.Shared.Services;
using TileShop.AvaloniaUI.Windowing;
using CommunityToolkit.Mvvm.ComponentModel;
using TileShop.AvaloniaUI.Imaging;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Threading.Tasks;

namespace TileShop.AvaloniaUI.ViewModels;

public partial class ImportImageViewModel : DialogViewModel<ImportImageViewModel>
{
    private readonly Arranger _arranger;
    private readonly IAsyncFileSelectService _fileSelect;

    private readonly IndexedImage? _originalIndexed;
    private readonly DirectImage? _originalDirect;

    private IndexedImage? _importedIndexed;
    private DirectImage? _importedDirect;

    [ObservableProperty] private string? _imageFileName;
    [ObservableProperty] private BitmapAdapter _originalSource;
    [ObservableProperty] private BitmapAdapter? _importedSource;
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
    [ObservableProperty] private string _importError = "";
    [ObservableProperty] protected int _zoom = 1;

    public bool IsTiledArranger => _arranger.Layout == ElementLayout.Tiled;
    public bool IsSingleArranger => _arranger.Layout == ElementLayout.Single;

    public ImportImageViewModel(Arranger arranger, IAsyncFileSelectService fileSelect)
    {
        _arranger = arranger;
        _fileSelect = fileSelect;

        if (_arranger.ColorType == PixelColorType.Indexed)
        {
            _originalIndexed = new IndexedImage(_arranger);
            _originalSource = new IndexedBitmapAdapter(_originalIndexed);
        }
        else if (_arranger.ColorType == PixelColorType.Direct)
        {
            _originalDirect = new DirectImage(_arranger);
            _originalSource = new DirectBitmapAdapter(_originalDirect);
        }
        else
        {
            throw new ArgumentException($"Invalid color type for '{arranger.Name}': {arranger.ColorType}");
        }

        Title = "Import Image Into Arranger";
    }

    //protected override void OnViewLoaded()
    //{
    //    base.OnViewLoaded();

    //    BrowseForImportFile();
    //}

    [RelayCommand]
    public async Task BrowseForImportFile()
    {
        var fileName = await _fileSelect.GetImportArrangerFileNameByUserAsync();

        if (fileName is null)
            return;

        ImageFileName = fileName.LocalPath;

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
        if (_arranger.ColorType == PixelColorType.Indexed && _importedIndexed is not null)
            _importedIndexed.SaveImage();
        else if (_arranger.ColorType == PixelColorType.Direct && _importedDirect is not null)
            _importedDirect.SaveImage();

        base.Ok(result);
    }
}
