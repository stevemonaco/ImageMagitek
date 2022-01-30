using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using TileShop.Shared.Services;

namespace TileShop.AvaloniaUI.Services;

internal class FileSelectService : IFileSelectService
{
    public string? GetProjectFileNameByUser()
    {
        var filters = new List<FileDialogFilter>
        {
            new FileDialogFilter { Extensions = { "xml" }, Name = "Project Files" }
        };

        var dialog = new OpenFileDialog
        {
            Title = "Select Project File",
            AllowMultiple = false,
            Filters = filters
        };

        var window = GetWindow();

        if (window is not null)
            return dialog.ShowAsync(window).Result?.FirstOrDefault();

        return null;
    }

    public string? GetNewProjectFileNameByUser()
    {
        var filters = new List<FileDialogFilter>
        {
            new FileDialogFilter { Extensions = { "xml" }, Name = "Project Files" }
        };

        var dialog = new SaveFileDialog
        {
            Title = "Create New Project File",
            DefaultExtension = ".xml",
            Filters = filters
        };

        var window = GetWindow();

        if (window is not null)
            return dialog.ShowAsync(window).Result;

        return null;
    }

    public string? GetExistingDataFileNameByUser()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select File",
            AllowMultiple = false
        };

        var window = GetWindow();

        if (window is not null)
            return dialog.ShowAsync(window).Result?.FirstOrDefault();

        return null;
    }

    public string? GetExportArrangerFileNameByUser(string defaultName)
    {
        var filters = new List<FileDialogFilter>
        {
            new FileDialogFilter { Extensions = { "png" }, Name = "PNG Image" }
        };

        var dialog = new SaveFileDialog
        {
            InitialFileName = defaultName,
            Title = "Export Arranger As",
            DefaultExtension = ".png",
            Filters = filters
        };

        var window = GetWindow();

        if (window is not null)
            return dialog.ShowAsync(window).Result;

        return null;
    }

    public string? GetImportArrangerFileNameByUser()
    {
        var filters = new List<FileDialogFilter>
        {
            new FileDialogFilter { Extensions = { "png" }, Name = "PNG Image" }
        };

        var dialog = new OpenFileDialog
        {
            Title = "Import Image to Arranger",
            AllowMultiple = false,
            Filters = filters
        };

        var window = GetWindow();

        if (window is not null)
            return dialog.ShowAsync(window).Result?.FirstOrDefault();

        return null;
    }

    private static Window? GetWindow()
    {
        var lifetime = Avalonia.Application.Current!.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        return lifetime?.MainWindow;
    }
}
