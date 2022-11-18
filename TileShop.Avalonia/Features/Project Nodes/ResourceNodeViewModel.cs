using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using ImageMagitek.Project;

namespace TileShop.AvaloniaUI.ViewModels;

public abstract partial class ResourceNodeViewModel : ObservableObject
{
    public required ResourceNode Node { get; set; }
    public ResourceNodeViewModel? ParentModel { get; set; }
    public abstract int SortPriority { get; }

    [ObservableProperty] private ObservableCollection<ResourceNodeViewModel> _children = new();
    [ObservableProperty] private bool _isExpanded;
    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private string _name = "";

    public void NotifyChildrenChanged()
    {
        OnPropertyChanged(nameof(Children));
    }

    public void OpenInFolder()
    {

    }
}
