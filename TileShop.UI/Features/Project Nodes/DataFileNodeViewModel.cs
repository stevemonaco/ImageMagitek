using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Mvvm.ComponentModel;
using ImageMagitek;
using ImageMagitek.Project;

namespace TileShop.UI.ViewModels;

public partial class DataFileNodeViewModel : ResourceNodeViewModel
{
    public override int SortPriority => 2;

    [ObservableProperty] private bool _isMissing;

    [SetsRequiredMembers]
    public DataFileNodeViewModel(ResourceNode node, ResourceNodeViewModel? parent)
    {
        Node = node;
        Name = node.Name;
        ParentModel = parent;
        IsMissing = node.Item is FileDataSource { IsMissing: true };
    }
}

/// <summary>
/// Root of a data file opened without a project
/// </summary>
public sealed class StandaloneFileNodeViewModel : DataFileNodeViewModel
{
    [SetsRequiredMembers]
    public StandaloneFileNodeViewModel(ResourceNode root) : base(root, null)
    {
    }
}
