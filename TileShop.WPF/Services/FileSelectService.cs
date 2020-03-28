using Microsoft.Win32;

namespace TileShop.WPF.Services
{
    public interface IFileSelectService
    {
        string GetProjectFileNameByUser();
        string GetNewProjectFileNameByUser();
        string GetExistingDataFileNameByUser();
        string GetExportArrangerFileNameByUser(string defaultName);
    }

    public class FileSelectService : IFileSelectService
    {
        public string GetProjectFileNameByUser()
        {
            var ofd = new OpenFileDialog();

            ofd.Title = "Select Project File";
            ofd.ValidateNames = true;
            ofd.CheckFileExists = true;
            ofd.AddExtension = true;
            ofd.Filter = "Project Files|*.xml";

            if (ofd.ShowDialog().Value)
                return ofd.FileName;

            return null;
        }

        public string GetNewProjectFileNameByUser()
        {
            var sfd = new SaveFileDialog();

            sfd.Title = "Create New Project File";
            sfd.DefaultExt = ".xml";
            sfd.Filter = "Project Files|*.xml";
            sfd.ValidateNames = true;

            if (sfd.ShowDialog().Value)
                return sfd.FileName;

            return null;
        }

        public string GetExistingDataFileNameByUser()
        {
            var ofd = new OpenFileDialog();

            ofd.Title = "Select File";
            ofd.ValidateNames = true;
            ofd.CheckFileExists = true;

            if (ofd.ShowDialog().Value)
                return ofd.FileName;

            return null;
        }

        public string GetExportArrangerFileNameByUser(string defaultName)
        {
            var sfd = new SaveFileDialog();

            sfd.FileName = defaultName;
            sfd.Title = "Export Arranger As";
            sfd.ValidateNames = true;
            sfd.DefaultExt = ".bmp";
            sfd.Filter = "Bitmap Image|*.bmp";

            if (sfd.ShowDialog() is true)
                return sfd.FileName;

            return null;
        }
    }
}
