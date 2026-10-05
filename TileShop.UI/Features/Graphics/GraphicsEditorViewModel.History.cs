using CommunityToolkit.Mvvm.Input;
using TileShop.Shared.Models;
using TileShop.Shared.Tools;
using TileShop.UI.Features.Graphics;

namespace TileShop.UI.ViewModels;

public partial class GraphicsEditorViewModel
{
    private GraphicsEditHistory _history = null!;

    public override void ApplyHistoryAction(HistoryAction action) => GraphicsEditHistory.Apply(action, _imageAdapter);

    public override void AddHistoryAction(HistoryAction action)
    {
        _history.Add(action, WorkingArranger);
        NotifyHistoryChanged();
    }

    private void ClearHistory()
    {
        _history.Reset(WorkingArranger);
        NotifyHistoryChanged();
    }

    private void NotifyHistoryChanged()
    {
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
    }

    [RelayCommand]
    public override void Undo()
    {
        if (!CanUndo)
            return;

        if (_history.Undo(_imageAdapter))
            OnWorkingArrangerReplaced();

        NotifyHistoryChanged();
        IsModified = CanUndo;
        InvalidateEditor(InvalidationLevel.Display);
    }

    [RelayCommand]
    public override void Redo()
    {
        if (!CanRedo)
            return;

        if (_history.Redo(_imageAdapter))
            OnWorkingArrangerReplaced();

        NotifyHistoryChanged();
        IsModified = true;
        InvalidateEditor(InvalidationLevel.Display);
    }
}
