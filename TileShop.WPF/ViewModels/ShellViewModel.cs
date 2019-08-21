using Caliburn.Micro;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace TileShop.WPF.ViewModels
{
    public class ShellViewModel : Conductor<object>
    {
        private MenuViewModel _activeMenu;
        public MenuViewModel ActiveMenu
        {
            get =>_activeMenu;
            set
            {
                _activeMenu = value;
                NotifyOfPropertyChange(() => ActiveMenu);
            }
        }

        private ProjectTreeViewModel _activeTree;
        public ProjectTreeViewModel ActiveTree
        {
            get { return _activeTree; }
            set { _activeTree = value; }
        }


        public ShellViewModel(MenuViewModel activeMenu, ProjectTreeViewModel activeTree)
        {
            ActiveMenu = activeMenu;
            ActiveTree = activeTree;
        }
    }
}
