using Stylet;
using ImageMagitek.Project;
using TileShop.Shared.EventModels;
using TileShop.WPF.Models;

namespace TileShop.WPF.ViewModels
{
    public abstract class ResourceEditorBaseViewModel : ToolViewModel, IHandle<ResourceRenamedEvent>
    {
        public IProjectResource Resource { get; protected set; }

        private BindableCollection<HistoryAction> _undoHistory = new BindableCollection<HistoryAction>();
        public BindableCollection<HistoryAction> UndoHistory
        {
            get => _undoHistory;
            set => SetAndNotify(ref _undoHistory, value);
        }

        private BindableCollection<HistoryAction> _redoHistory = new BindableCollection<HistoryAction>();
        public BindableCollection<HistoryAction> RedoHistory
        {
            get => _redoHistory;
            set => SetAndNotify(ref _redoHistory, value);
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
            NotifyOfPropertyChange(() => CanUndo);
            NotifyOfPropertyChange(() => CanRedo);
        }

        public virtual void Handle(ResourceRenamedEvent message)
        {
            if (ReferenceEquals(Resource, message.Resource))
                DisplayName = message.NewName;
        }
    }
}
