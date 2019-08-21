using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;

namespace TileShop.WPF.Services
{
    public interface IFileSelectService
    {
        string GetProjectByUser();
    }

    public class FileSelectService : IFileSelectService
    {
        public string GetProjectByUser()
        {
            var ofd = new OpenFileDialog();

            ofd.Title = "Select Project File";
            ofd.ValidateNames = true;
            ofd.CheckFileExists = true;
            ofd.AddExtension = true;
            ofd.Filter = "Image Files|*.xml";

            if (ofd.ShowDialog() == DialogResult.OK)
                return ofd.FileName;

            return null;
        }
    }
}
