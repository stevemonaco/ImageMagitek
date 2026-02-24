using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Dock.Model.Mvvm.Controls;

namespace TileShop.UI.ViewModels;

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

        UpdateTitle();
        _editor.PropertyChanged += Editor_PropertyChanged;
    }

    private void Editor_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ToolViewModel.DisplayName) or nameof(ToolViewModel.IsModified))
            UpdateTitle();
    }

    private void UpdateTitle()
    {
        Title = _editor.IsModified ? $"{_editor.DisplayName} *" : _editor.DisplayName;
    }

    public override bool OnClose()
    {
        using var cts = new CancellationTokenSource();
        bool result = default;

        _editors.RequestSaveUserChanges(_editor, true).ContinueWith(x =>
            {
                result = x.IsCompletedSuccessfully && x.Result != UserSaveAction.Cancel;
                cts.Cancel();
            },
            TaskScheduler.FromCurrentSynchronizationContext());

        Dispatcher.UIThread.MainLoop(cts.Token);

        if (result)
            _editors.Editors.Remove(_editor);

        return result;
    }
}
