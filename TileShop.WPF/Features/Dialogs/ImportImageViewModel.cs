﻿using System;
using Stylet;
using ImageMagitek;
using ImageMagitek.Colors;
using TileShop.WPF.Imaging;
using TileShop.Shared.Services;

namespace TileShop.WPF.ViewModels;

public class ImportImageViewModel : Screen
{
    private readonly Arranger _arranger;
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

    private BitmapAdapter _originalSource;
    public BitmapAdapter OriginalSource
    {
        get => _originalSource;
        set => SetAndNotify(ref _originalSource, value);
    }

    private BitmapAdapter _importedSource;
    public BitmapAdapter ImportedSource
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
            {
                if (_arranger.ColorType == PixelColorType.Indexed)
                    ImportIndexed(ImageFileName);
                else if (_arranger.ColorType == PixelColorType.Direct)
                    ImportDirect(ImageFileName);
            }
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
    }

    protected override void OnViewLoaded()
    {
        base.OnViewLoaded();

        BrowseForImportFile();
    }

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
