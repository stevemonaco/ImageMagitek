using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Dock.Model.Mvvm.Controls;

namespace TileShop.AvaloniaUI.ViewModels;

public partial class DockableEditorViewModel : Document
{
    [ObservableProperty] private ResourceEditorBaseViewModel _editor;
    private readonly EditorsViewModel _editors;

    public DockableEditorViewModel(ResourceEditorBaseViewModel editor, EditorsViewModel editors)
    {
        _editor = editor;
        _editors = editors;

        CanClose = true;
        CanFloat = true;
    }

    public override bool OnClose()
    {
        var userAction = _editors.RequestSaveUserChanges(_editor, true);

        if (userAction == UserSaveAction.Cancel)
        {
            return false;
        }
        else
        {
            _editors.Editors.Remove(_editor);
            _editors.ActiveEditor = _editors.Editors.FirstOrDefault();

            return true;
        }
    }
}
