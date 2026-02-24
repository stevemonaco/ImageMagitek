using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using ImageMagitek.Project;
using TileShop.Shared.Messages;
using TileShop.Shared.Models;

namespace TileShop.UI.ViewModels;

public abstract partial class ResourceEditorBaseViewModel : ToolViewModel
{
    /// <summary>
    /// Resource that is being edited
    /// </summary>
    public IProjectResource Resource { get; protected set; }

    /// <summary>
    /// Resource instance contained by ProjectTree
    /// </summary>
    public IProjectResource OriginatingProjectResource { get; init; }

    [ObservableProperty] private string _activityMessage = "";
    [ObservableProperty] private string _pendingOperationMessage = "";
    [ObservableProperty] private ObservableCollection<HistoryAction> _undoHistory = new();
    [ObservableProperty] private ObservableCollection<HistoryAction> _redoHistory = new();

    public ResourceEditorBaseViewModel(IProjectResource resource)
    {
        Resource = resource;
        OriginatingProjectResource = resource;
        Messenger.Register<ResourceRenamedMessage>(this, Handle);
    }

    public virtual bool CanUndo { get => UndoHistory.Count > 0; }
    public virtual bool CanRedo { get => RedoHistory.Count > 0; }

    public abstract void Undo();
    public abstract void Redo();

    public abstract void ApplyHistoryAction(HistoryAction action);
    public virtual void AddHistoryAction(HistoryAction action)
    {
        UndoHistory.Add(action);
        RedoHistory.Clear();
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
    }

    public virtual void Handle(object recipient, ResourceRenamedMessage message)
    {
        if (ReferenceEquals(Resource, message.Resource))
            DisplayName = message.NewName;
    }
}
