using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using ImageMagitek.Project;
using TileShop.Shared.Models;
using TileShop.Shared.Messages;

namespace TileShop.AvaloniaUI.ViewModels;

public abstract partial class ResourceEditorBaseViewModel : ToolViewModel
{
    public IProjectResource Resource { get; protected set; }
    public IProjectResource? OriginatingProjectResource { get; set; }

    [ObservableProperty] private string _activityMessage = "";
    [ObservableProperty] private string _pendingOperationMessage = "";
    [ObservableProperty] private ObservableCollection<HistoryAction> _undoHistory = new();
    [ObservableProperty] private ObservableCollection<HistoryAction> _redoHistory = new();

    public ResourceEditorBaseViewModel(IProjectResource resource)
    {
        Resource = resource;
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
