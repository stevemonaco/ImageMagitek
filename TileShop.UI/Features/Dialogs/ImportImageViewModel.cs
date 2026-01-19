using ImageMagitek;
using ImageMagitek.Colors;
using CommunityToolkit.Mvvm.ComponentModel;
using TileShop.UI.Imaging;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Threading.Tasks;
using TileShop.Shared.Interactions;

namespace TileShop.UI.ViewModels;
public partial class ImportImageViewModel : RequestViewModel<ImportImageViewModel>
{
    private readonly Arranger _arranger;
    private readonly IAsyncFileRequestService _fileSelect;

    private readonly IndexedImage? _originalIndexed;
    private readonly DirectImage? _originalDirect;

    private IndexedImage? _previewIndexed;
    private DirectImage? _previewDirect;

    [ObservableProperty] private string? _imageFileName;
    [ObservableProperty] private BitmapAdapter _originalSource;
    [ObservableProperty] private BitmapAdapter? _previewSource;
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

    public ImportImageViewModel(Arranger arranger, IAsyncFileRequestService fileSelect)
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
        AcceptName = "Import";
    }

    //protected override void OnViewLoaded()
    //{
    //    base.OnViewLoaded();

    //    BrowseForImportFile();
    //}

    [RelayCommand]
    public async Task BrowseForImportFile()
    {
        var fileName = await _fileSelect.RequestImportArrangerFileName();

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
        _previewIndexed = new IndexedImage(_arranger);

        var result = _previewIndexed.TryImportImage(fileName, new ImageSharpFileAdapter(), matchStrategy);

        result.Switch(
            success =>
            {
                ImageFileName = fileName;
                ImportError = string.Empty;
                CanImport = true;
                PreviewSource = new IndexedBitmapAdapter(_previewIndexed);
            },
            fail =>
            {
                CanImport = false;
                ImageFileName = fileName;
                PreviewSource = null;
                ImportError = fail.Reason;
            });
    }

    private void ImportDirect(string fileName)
    {
        _previewDirect = new DirectImage(_arranger);
        _previewDirect.ImportImage(fileName, new ImageSharpFileAdapter());
        PreviewSource = new DirectBitmapAdapter(_previewDirect);
        CanImport = true;
    }

    public override ImportImageViewModel? ProduceResult() => this;

    protected override Task<bool> OnAccepted()
    {
        if (_arranger.ColorType == PixelColorType.Indexed && _previewIndexed is not null)
            _previewIndexed.SaveImage();
        else if (_arranger.ColorType == PixelColorType.Direct && _previewDirect is not null)
            _previewDirect.SaveImage();
        
        return Task.FromResult(true);
    }
}
