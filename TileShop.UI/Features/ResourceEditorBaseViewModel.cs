using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using ImageMagitek.Project;
using TileShop.Shared.Models;
using TileShop.UI.Models;

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

    /// <summary>
    /// Hotkeys that apply window-wide while this editor is active
    /// </summary>
    public virtual IReadOnlyList<Hotkey> Hotkeys => [];

    /// <summary>
    /// Commands the Edit menu runs while this editor is active
    /// </summary>
    public virtual EditCommands EditCommands => EditCommands.None;

    public ResourceEditorBaseViewModel(IProjectResource resource)
    {
        Resource = resource;
        OriginatingProjectResource = resource;
    }

    /// <summary>
    /// Whether this editor edits one of <paramref name="resources"/>, as its resource or the tree resource it was opened from
    /// </summary>
    public bool EditsAny(IReadOnlySet<IProjectResource> resources) =>
        resources.Contains(Resource) || resources.Contains(OriginatingProjectResource);

    /// <summary>
    /// Whether this editor reads any of <paramref name="resources"/>, so it goes stale when they are replaced or removed
    /// </summary>
    public virtual bool DependsOn(IReadOnlySet<IProjectResource> resources) => EditsAny(resources);

    /// <summary>
    /// Undoes state this editor set on shared resources, before it is closed or replaced
    /// </summary>
    public virtual void OnClosing() { }

    public virtual bool CanUndo { get => UndoHistory.Count > 0; }
    public virtual bool CanRedo { get => RedoHistory.Count > 0; }

    public abstract void Undo();
    public abstract void Redo();

    public virtual void AddHistoryAction(HistoryAction action)
    {
        UndoHistory.Add(action);
        RedoHistory.Clear();
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
    }
}
