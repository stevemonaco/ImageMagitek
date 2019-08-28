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
            get => _activeTree;
            set 
            { 
                _activeTree = value;
                NotifyOfPropertyChange(() => ActiveTree);
            }
        }

        private StatusBarViewModel _activeStatusBar;
        public StatusBarViewModel ActiveStatusBar
        {
            get => _activeStatusBar;
            set
            {
                _activeStatusBar = value;
                NotifyOfPropertyChange(() => ActiveStatusBar);
            }
        }

        private EditorHostViewModel _activeEditorHost;
        public EditorHostViewModel ActiveEditorHost
        {
            get { return _activeEditorHost; }
            set 
            {
                _activeEditorHost = value;
                NotifyOfPropertyChange(() => ActiveEditorHost);
            }
        }

        public ShellViewModel(MenuViewModel activeMenu, ProjectTreeViewModel activeTree, 
            StatusBarViewModel activeStatusBar, EditorHostViewModel activeEditorHost)
        {
            ActiveMenu = activeMenu;
            ActiveTree = activeTree;
            ActiveStatusBar = activeStatusBar;
            ActiveEditorHost = activeEditorHost;
        }
    }
}
