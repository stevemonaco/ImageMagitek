using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using TileShop.Shared.Services;

namespace TileShop.AvaloniaUI.Services;

internal class AsyncFileSelectService : IAsyncFileSelectService
{
    private static FilePickerFileType _projectType = new("XML Project")
    {
        Patterns = new[] { "*.xml" },
        AppleUniformTypeIdentifiers = new[] { "public.xml" },
        MimeTypes = new[] { "text/xml" }
    };

    public async Task<Uri?> GetProjectFileNameByUserAsync()
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

    public async Task<Uri?> GetNewProjectFileNameByUserAsync()
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

    public async Task<Uri?> GetExistingDataFileNameByUserAsync()
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

    public async Task<Uri?> GetExportArrangerFileNameByUserAsync(string defaultName)
    {
        var options = new FilePickerSaveOptions()
        {
            SuggestedFileName = defaultName,
            Title = "Export Arranger As"
        };

        return await SaveFilePickerAsync(options);
    }

    public async Task<Uri?> GetImportArrangerFileNameByUserAsync()
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

    private static async Task<Uri?> OpenFilePickerAsync(FilePickerOpenOptions options)
    {
        var window = GetWindow();

        if (window is null)
            return null;

        var pickerResult = await window.StorageProvider.OpenFilePickerAsync(options);

        if (pickerResult?[0].TryGetUri(out var uri) is true)
        {
            return uri;
        }

        return null;
    }

    private static async Task<Uri?> SaveFilePickerAsync(FilePickerSaveOptions options)
    {
        var window = GetWindow();

        if (window is null)
            return null;

        var pickerResult = await window.StorageProvider.SaveFilePickerAsync(options);

        if (pickerResult?.TryGetUri(out var uri) is true)
        {
            return uri;
        }

        return null;
    }

    private static Window? GetWindow()
    {
        var lifetime = Avalonia.Application.Current!.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        return lifetime?.MainWindow;
    }
}
