using Stylet;
using System;
using System.Collections.Generic;
using System.Text;

namespace TileShop.WPF.ViewModels
{
    public class RenameNodeViewModel : Screen
    {
        private TreeNodeViewModel _nodeModel;

        private string _name;
        public string Name
        {
            get => _name;
            set => SetAndNotify(ref _name, value);
        }

        public RenameNodeViewModel(TreeNodeViewModel nodeModel)
        {
            _nodeModel = nodeModel;
            Name = nodeModel.Name;
        }

        public void Rename()
        {
            RequestClose(true);
        }

        public void Cancel() => RequestClose(false);
    }
}
