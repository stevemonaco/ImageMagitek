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
        Title = _editor.DisplayName;
    }

    /// <summary>
    /// True while Dock is closing this tab, so the factory leaves its removal to Dock
    /// </summary>
    internal bool IsClosing { get; private set; }

    public override bool OnClose()
    {
        using var cts = new CancellationTokenSource();
        bool result = default;

        IsClosing = true;
        try
        {
            _editors.CloseEditor(_editor).ContinueWith(x =>
                {
                    result = x.IsCompletedSuccessfully && x.Result;
                    cts.Cancel();
                },
                TaskScheduler.FromCurrentSynchronizationContext());

            Dispatcher.UIThread.MainLoop(cts.Token);
        }
        finally
        {
            IsClosing = false;
        }

        return result;
    }
}
