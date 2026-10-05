using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using TileShop.Shared.Interactions;

namespace TileShop.UI.Services;

internal class AsyncFileRequestService : IAsyncFileRequestService
{
    private static FilePickerFileType _projectType = new("XML Project")
    {
        Patterns = new[] { "*.xml" },
        AppleUniformTypeIdentifiers = new[] { "public.xml" },
        MimeTypes = new[] { "text/xml" }
    };

    private static FilePickerFileType _jascPaletteType = new("JASC Palette")
    {
        Patterns = new[] { "*.pal" }
    };

    private static FilePickerFileType _gimpPaletteType = new("GIMP Palette")
    {
        Patterns = new[] { "*.gpl" }
    };

    public async Task<Uri?> RequestProjectFileName()
    {
        var options = new FilePickerOpenOptions()
        {
            FileTypeFilter = new List<FilePickerFileType>()
            {
                _projectType
            },
            Title = "Select Project File"
        };

        return await OpenFilePickerAsync(options);
    }

    public async Task<Uri?> RequestNewProjectFileName()
    {
        var options = new FilePickerSaveOptions()
        {
            FileTypeChoices = new List<FilePickerFileType>()
            {
                _projectType
            },
            Title = "Create New Project File"
        };

        return await SaveFilePickerAsync(options);
    }

    public async Task<Uri?> RequestExistingDataFileName()
    {
        var options = new FilePickerOpenOptions()
        {
            FileTypeFilter = new List<FilePickerFileType>()
            {
                FilePickerFileTypes.All
            },
            Title = "Select Project File"
        };

        return await OpenFilePickerAsync(options);
    }

    public async Task<Uri?> RequestExportArrangerFileName(string defaultName)
    {
        var options = new FilePickerSaveOptions()
        {
            SuggestedFileName = defaultName,
            Title = "Export Arranger As"
        };

        return await SaveFilePickerAsync(options);
    }

    public async Task<Uri?> RequestImportArrangerFileName()
    {
        var options = new FilePickerOpenOptions()
        {
            FileTypeFilter = new List<FilePickerFileType>()
            {
                FilePickerFileTypes.ImagePng
            },
            Title = "Import Image into Arranger"
        };

        return await OpenFilePickerAsync(options);
    }

    public async Task<Uri?> RequestExportPaletteFileName(string defaultName)
    {
        var options = new FilePickerSaveOptions()
        {
            SuggestedFileName = defaultName,
            DefaultExtension = "pal",
            FileTypeChoices = new List<FilePickerFileType>()
            {
                _jascPaletteType,
                _gimpPaletteType
            },
            Title = "Export Palette As"
        };

        return await SaveFilePickerAsync(options);
    }

    public async Task<Uri?> RequestImportPaletteFileName()
    {
        var options = new FilePickerOpenOptions()
        {
            FileTypeFilter = new List<FilePickerFileType>()
            {
                new("Palette Files") { Patterns = new[] { "*.pal", "*.gpl" } },
                _jascPaletteType,
                _gimpPaletteType
            },
            Title = "Import Palette"
        };

        return await OpenFilePickerAsync(options);
    }

    private static async Task<Uri?> OpenFilePickerAsync(FilePickerOpenOptions options)
    {
        var window = MainWindowLocator.GetMainWindow();

        if (window is null)
            return null;

        var pickerResult = await window.StorageProvider.OpenFilePickerAsync(options);

        return pickerResult.FirstOrDefault()?.Path;
    }

    private static async Task<Uri?> SaveFilePickerAsync(FilePickerSaveOptions options)
    {
        var window = MainWindowLocator.GetMainWindow();

        if (window is null)
            return null;

        var pickerResult = await window.StorageProvider.SaveFilePickerAsync(options);
        return pickerResult?.Path;
    }
}
