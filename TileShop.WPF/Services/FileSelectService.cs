using Microsoft.Win32;
using TileShop.Shared.Services;

namespace TileShop.WPF.Services;

public class FileSelectService : IFileSelectService
{
    public string GetProjectFileNameByUser()
    {
        var ofd = new OpenFileDialog
        {
            Title = "Select Project File",
            ValidateNames = true,
            CheckFileExists = true,
            AddExtension = true,
            Filter = "Project Files|*.xml"
        };

        if (ofd.ShowDialog().Value)
            return ofd.FileName;

        return null;
    }

    public string GetNewProjectFileNameByUser()
    {
        var sfd = new SaveFileDialog
        {
            Title = "Create New Project File",
            DefaultExt = ".xml",
            Filter = "Project Files|*.xml",
            ValidateNames = true
        };

        if (sfd.ShowDialog().Value)
            return sfd.FileName;

        return null;
    }

    public string GetExistingDataFileNameByUser()
    {
        var ofd = new OpenFileDialog
        {
            Title = "Select File",
            ValidateNames = true,
            CheckFileExists = true
        };

        if (ofd.ShowDialog().Value)
            return ofd.FileName;

        return null;
    }

    public string GetExportArrangerFileNameByUser(string defaultName)
    {
        var sfd = new SaveFileDialog
        {
            FileName = defaultName,
            Title = "Export Arranger As",
            ValidateNames = true,
            DefaultExt = ".png",
            Filter = "PNG Image|*.png"
        };

        if (sfd.ShowDialog() is true)
            return sfd.FileName;

        return null;
    }

    public string GetImportArrangerFileNameByUser()
    {
        var ofd = new OpenFileDialog
        {
            Title = "Import Image to Arranger",
            ValidateNames = true,
            DefaultExt = ".png",
            Filter = "PNG Image|*.png",
            CheckFileExists = true
        };

        if (ofd.ShowDialog().Value)
            return ofd.FileName;

        return null;
    }
}
